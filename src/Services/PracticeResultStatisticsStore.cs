using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

public sealed class PracticeResultStatisticsStore : IPracticeResultStatisticsStore
{
    private readonly string _databasePath;

    public PracticeResultStatisticsStore()
    {
        var preferredConfigPath = ConfigurationPaths.GetPreferredPerUserConfigPath();
        var appDirectory = preferredConfigPath is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PentaGrammata")
            : Path.GetDirectoryName(preferredConfigPath) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PentaGrammata");

        Directory.CreateDirectory(appDirectory);
        _databasePath = Path.Combine(appDirectory, "practice-results.db");
    }

    public async Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);

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
                error_rate_percent REAL NOT NULL
            );
            """;

        await createCommand.ExecuteNonQueryAsync(cancellationToken);

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO practice_result_statistics (
                recorded_at,
                character_wpm,
                average_wpm,
                character_count,
                error_count,
                error_rate_percent
            )
            VALUES (
                $recorded_at,
                $character_wpm,
                $average_wpm,
                $character_count,
                $error_count,
                $error_rate_percent
            );
            """;

        insertCommand.Parameters.AddWithValue("$recorded_at", record.RecordedAt.ToString("O"));
        insertCommand.Parameters.AddWithValue("$character_wpm", record.CharacterWpm);
        insertCommand.Parameters.AddWithValue("$average_wpm", record.AverageWpm);
        insertCommand.Parameters.AddWithValue("$character_count", record.CharacterCount);
        insertCommand.Parameters.AddWithValue("$error_count", record.ErrorCount);
        insertCommand.Parameters.AddWithValue("$error_rate_percent", record.ErrorRatePercent);

        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
