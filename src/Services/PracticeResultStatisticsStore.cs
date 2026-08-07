using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PentaGrammata.Configuration;
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

    public async Task<IReadOnlyList<PracticeTrendPoint>> GetTrendPointsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    recorded_at,
                    character_wpm,
                    average_wpm,
                    error_rate_percent,
                    error_threshold_percent,
                    noise_level_db
                FROM practice_result_statistics
                ORDER BY recorded_at ASC;
                """;

            var points = new List<PracticeTrendPoint>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                points.Add(new PracticeTrendPoint
                {
                    RecordedAt = DateTimeOffset.Parse(reader.GetString(0)),
                    CharacterWpm = reader.GetInt32(1),
                    AverageWpm = reader.GetInt32(2),
                    ErrorRatePercent = reader.GetDouble(3),
                    ErrorThresholdPercent = reader.GetDouble(4),
                    NoiseLevelDb = reader.GetDouble(5)
                });
            }

            return points;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read trend points from {DatabasePath}", _databasePath);
            throw new StatisticsStoreException("Could not read trend points.", ex);
        }
    }

    public async Task<IReadOnlyList<PracticeResultStatisticsRecord>> GetStatisticsRecordsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    recorded_at,
                    character_wpm,
                    average_wpm,
                    character_count,
                    error_count,
                    error_rate_percent,
                    error_threshold_percent,
                    noise_type,
                    noise_level_db,
                    noise_bandwidth_hz,
                    agc_enabled,
                    agc_delay_seconds,
                    apf_enabled,
                    apf_bandwidth_hz,
                    apf_peak_gain_db
                FROM practice_result_statistics
                ORDER BY recorded_at ASC;
                """;

            var records = new List<PracticeResultStatisticsRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                records.Add(new PracticeResultStatisticsRecord
                {
                    RecordedAt = DateTimeOffset.Parse(reader.GetString(0)),
                    CharacterWpm = reader.GetInt32(1),
                    AverageWpm = reader.GetInt32(2),
                    CharacterCount = reader.GetInt32(3),
                    ErrorCount = reader.GetInt32(4),
                    ErrorRatePercent = reader.GetDouble(5),
                    ErrorThresholdPercent = reader.GetDouble(6),
                    NoiseType = Enum.Parse<NoiseType>(reader.GetString(7), ignoreCase: true),
                    NoiseLevelDb = reader.GetDouble(8),
                    NoiseBandwidthHz = reader.GetDouble(9),
                    AgcEnabled = reader.GetInt32(10) != 0,
                    AgcDelaySeconds = reader.GetDouble(11),
                    ApfEnabled = reader.GetInt32(12) != 0,
                    ApfBandwidthHz = reader.GetDouble(13),
                    ApfPeakGainDb = reader.GetDouble(14),
                });
            }

            return records;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read statistics records from {DatabasePath}", _databasePath);
            throw new StatisticsStoreException("Could not read statistics records.", ex);
        }
    }

    public async Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    recorded_at,
                    expected_symbol,
                    actual_symbol,
                    distance,
                    count
                FROM practice_confusions
                ORDER BY recorded_at ASC;
                """;

            var observations = new List<ConfusionObservation>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                observations.Add(new ConfusionObservation
                {
                    RecordedAt = DateTimeOffset.Parse(reader.GetString(0)),
                    ExpectedSymbol = reader.GetString(1),
                    ActualSymbol = reader.GetString(2),
                    Distance = reader.GetInt32(3),
                    Count = reader.GetInt32(4)
                });
            }

            return observations;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read confusion observations from {DatabasePath}", _databasePath);
            throw new StatisticsStoreException("Could not read confusion observations.", ex);
        }
    }

    private async Task SaveCoreAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);

        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        using var transaction = connection.BeginTransaction();

        var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO practice_result_statistics (
                recorded_at,
                character_wpm,
                average_wpm,
                character_count,
                error_count,
                error_rate_percent,
                error_threshold_percent,
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
                $error_threshold_percent,
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
        insertCommand.Parameters.AddWithValue("$error_threshold_percent", record.ErrorThresholdPercent);
        insertCommand.Parameters.AddWithValue("$noise_type", record.NoiseType.ToString());
        insertCommand.Parameters.AddWithValue("$noise_level_db", record.NoiseLevelDb);
        insertCommand.Parameters.AddWithValue("$noise_bandwidth_hz", record.NoiseBandwidthHz);
        insertCommand.Parameters.AddWithValue("$agc_enabled", record.AgcEnabled ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$agc_delay_seconds", record.AgcDelaySeconds);
        insertCommand.Parameters.AddWithValue("$apf_enabled", record.ApfEnabled ? 1 : 0);
        insertCommand.Parameters.AddWithValue("$apf_bandwidth_hz", record.ApfBandwidthHz);
        insertCommand.Parameters.AddWithValue("$apf_peak_gain_db", record.ApfPeakGainDb);

        await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var result = await idCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var statisticsId = Convert.ToInt64(result);

        if (record.Confusions.Count > 0)
        {
            var confusionInsertCommand = connection.CreateCommand();
            confusionInsertCommand.Transaction = transaction;
            confusionInsertCommand.CommandText =
                """
                INSERT INTO practice_confusions (
                    statistics_id,
                    recorded_at,
                    expected_symbol,
                    actual_symbol,
                    distance,
                    count
                )
                VALUES (
                    $statistics_id,
                    $recorded_at,
                    $expected_symbol,
                    $actual_symbol,
                    $distance,
                    $count
                );
                """;

            var statisticsIdParameter = confusionInsertCommand.Parameters.Add("$statistics_id", SqliteType.Integer);
            var recordedAtParameter = confusionInsertCommand.Parameters.Add("$recorded_at", SqliteType.Text);
            var expectedSymbolParameter = confusionInsertCommand.Parameters.Add("$expected_symbol", SqliteType.Text);
            var actualSymbolParameter = confusionInsertCommand.Parameters.Add("$actual_symbol", SqliteType.Text);
            var distanceParameter = confusionInsertCommand.Parameters.Add("$distance", SqliteType.Integer);
            var countParameter = confusionInsertCommand.Parameters.Add("$count", SqliteType.Integer);

            foreach (var confusion in record.Confusions)
            {
                statisticsIdParameter.Value = statisticsId;
                recordedAtParameter.Value = confusion.RecordedAt.ToString("O");
                expectedSymbolParameter.Value = confusion.ExpectedSymbol;
                actualSymbolParameter.Value = confusion.ActualSymbol;
                distanceParameter.Value = confusion.Distance;
                countParameter.Value = confusion.Count;

                await confusionInsertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        transaction.Commit();
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
                    error_threshold_percent REAL NOT NULL DEFAULT 0,
                    noise_type TEXT NOT NULL,
                    noise_level_db REAL NOT NULL,
                    noise_bandwidth_hz REAL NOT NULL,
                    agc_enabled INTEGER NOT NULL,
                    agc_delay_seconds REAL NOT NULL,
                    apf_enabled INTEGER NOT NULL,
                    apf_bandwidth_hz REAL NOT NULL,
                    apf_peak_gain_db REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS practice_confusions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    statistics_id INTEGER NOT NULL,
                    recorded_at TEXT NOT NULL,
                    expected_symbol TEXT NOT NULL,
                    actual_symbol TEXT NOT NULL,
                    distance INTEGER NOT NULL,
                    count INTEGER NOT NULL,
                    FOREIGN KEY(statistics_id) REFERENCES practice_result_statistics(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_practice_statistics_recorded_at
                    ON practice_result_statistics(recorded_at);

                CREATE INDEX IF NOT EXISTS idx_practice_confusions_recorded_at
                    ON practice_confusions(recorded_at);

                CREATE INDEX IF NOT EXISTS idx_practice_confusions_symbols
                    ON practice_confusions(expected_symbol, actual_symbol);
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
