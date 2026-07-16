using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

// SqliteException, IOException and UnauthorizedAccessException are translated into
// StatisticsStoreException so callers never depend on the storage technology.

namespace PentaGrammata.Services;

public sealed class PracticeResultStatisticsStore : IPracticeResultStatisticsStore
{
    private readonly ILogger<PracticeResultStatisticsStore> _logger;
    private readonly string _databasePath;

    // Schema initialization runs once per process, not on every insert.
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaInitialized;

    public string DatabasePath => _databasePath;

    public PracticeResultStatisticsStore(IAppPaths appPaths, ILogger<PracticeResultStatisticsStore> logger)
    {
        _logger = logger;

        var appDirectory = appPaths.AppDataDirectory;
        Directory.CreateDirectory(appDirectory);
        _databasePath = Path.Combine(appDirectory, "practice-results.db");
    }

    public async Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveCoreAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save practice statistics to {DatabasePath}", _databasePath);
            throw new StatisticsStoreException("Could not save practice statistics.", ex);
        }
    }

    private async Task SaveCoreAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO practice_result_statistics (
                recorded_at,
                character_wpm,
                average_wpm,
                character_count,
                error_count,
                error_rate_percent,
                noise_type,
                noise_level_db,
                noise_bandwidth_hz,
                agc_enabled,
                agc_delay_seconds,
                apf_enabled,
                apf_bandwidth_hz,
                apf_peak_gain_db
            )
            VALUES (
                $recorded_at,
                $character_wpm,
                $average_wpm,
                $character_count,
                $error_count,
                $error_rate_percent,
                $noise_type,
                $noise_level_db,
                $noise_bandwidth_hz,
                $agc_enabled,
                $agc_delay_seconds,
                $apf_enabled,
                $apf_bandwidth_hz,
                $apf_peak_gain_db
            );
            """;

        insertCommand.Parameters.AddWithValue("$recorded_at", record.RecordedAt.ToString("O"));
        insertCommand.Parameters.AddWithValue("$character_wpm", record.CharacterWpm);
        insertCommand.Parameters.AddWithValue("$average_wpm", record.AverageWpm);
        insertCommand.Parameters.AddWithValue("$character_count", record.CharacterCount);
        insertCommand.Parameters.AddWithValue("$error_count", record.ErrorCount);
        insertCommand.Parameters.AddWithValue("$error_rate_percent", record.ErrorRatePercent);
        insertCommand.Parameters.AddWithValue("$noise_type", record.NoiseType.ToString());
        insertCommand.Parameters.AddWithValue("$noise_level_db", record.NoiseLevelDb);
        insertCommand.Parameters.AddWithValue("$noise_bandwidth_hz", record.NoiseBandwidthHz);
        insertCommand.Parameters.AddWithValue("$agc_enabled", record.AgcEnabled ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$agc_delay_seconds", record.AgcDelaySeconds);
        insertCommand.Parameters.AddWithValue("$apf_enabled", record.ApfEnabled ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$apf_bandwidth_hz", record.ApfBandwidthHz);
        insertCommand.Parameters.AddWithValue("$apf_peak_gain_db", record.ApfPeakGainDb);

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaInitialized)
        {
            return;
        }

        await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            var createCommand = connection.CreateCommand();
            createCommand.CommandText =
                """
                CREATE TABLE IF NOT EXISTS practice_result_statistics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    recorded_at TEXT NOT NULL,
                    character_wpm INTEGER NOT NULL,
                    average_wpm INTEGER NOT NULL,
                    character_count INTEGER NOT NULL,
                    error_count INTEGER NOT NULL,
                    error_rate_percent REAL NOT NULL,
                    noise_type TEXT NOT NULL,
                    noise_level_db REAL NOT NULL,
                    noise_bandwidth_hz REAL NOT NULL,
                    agc_enabled INTEGER NOT NULL,
                    agc_delay_seconds REAL NOT NULL,
                    apf_enabled INTEGER NOT NULL,
                    apf_bandwidth_hz REAL NOT NULL,
                    apf_peak_gain_db REAL NOT NULL
                );
                """;

            await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _schemaInitialized = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}
