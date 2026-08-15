using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Stores;

namespace PentaGrammata.Tests.Stores;

// These tests are only possible because the store takes its data location from an
// injected IAppPaths rather than deriving a real per-user path itself.
[TestClass]
public sealed class PracticeResultStatisticsStoreTests
{
    private string _tempDirectory = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PentaGrammataTests", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void TearDown()
    {
        // The store opens pooled SQLite connections; release the file handles the pool
        // holds before deleting, otherwise the directory delete fails on Windows.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void DatabasePath_IsUnderInjectedAppDataDirectory()
    {
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());

        Assert.AreEqual(Path.Combine(_tempDirectory, "practice-results.db"), sut.DatabasePath);
    }

    [TestMethod]
    public async Task SaveAsync_CreatesDatabaseAndPersistsRecord()
    {
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());

        await sut.SaveAsync(CreateRecord());

        Assert.IsTrue(File.Exists(sut.DatabasePath));
        Assert.AreEqual(1, await CountRowsAsync(sut.DatabasePath));
    }

    [TestMethod]
    public async Task SaveAsync_AppendsAcrossMultipleCalls()
    {
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());

        await sut.SaveAsync(CreateRecord());
        await sut.SaveAsync(CreateRecord());

        Assert.AreEqual(2, await CountRowsAsync(sut.DatabasePath));
    }

    [TestMethod]
    public async Task GetStatisticsRecordsAsync_RoundTripsAllFieldsAndConfusions()
    {
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());
        var record = CreateRecord();
        record = new PracticeResultStatisticsRecord
        {
            RecordedAt = record.RecordedAt,
            CharacterWpm = record.CharacterWpm,
            AverageWpm = record.AverageWpm,
            CharacterCount = record.CharacterCount,
            ErrorCount = record.ErrorCount,
            ErrorRatePercent = record.ErrorRatePercent,
            ErrorThresholdPercent = 5.0,
            NoiseType = record.NoiseType,
            NoiseLevelDb = record.NoiseLevelDb,
            NoiseBandwidthHz = record.NoiseBandwidthHz,
            AgcEnabled = record.AgcEnabled,
            AgcDelaySeconds = record.AgcDelaySeconds,
            AgcMaxGainDb = 18.0,
            ApfEnabled = record.ApfEnabled,
            ApfBandwidthHz = record.ApfBandwidthHz,
            ApfPeakGainDb = record.ApfPeakGainDb,
            Confusions = new[]
            {
                new ConfusionObservation
                {
                    RecordedAt = DateTimeOffset.UnixEpoch,
                    ExpectedSymbol = "A",
                    ActualSymbol = "B",
                    Distance = 1,
                    Count = 2,
                },
            },
        };

        await sut.SaveAsync(record);

        var records = await sut.GetStatisticsRecordsAsync();
        Assert.HasCount(1, records);
        var actual = records[0];
        Assert.AreEqual(record.RecordedAt, actual.RecordedAt);
        Assert.AreEqual(record.CharacterWpm, actual.CharacterWpm);
        Assert.AreEqual(record.AverageWpm, actual.AverageWpm);
        Assert.AreEqual(record.CharacterCount, actual.CharacterCount);
        Assert.AreEqual(record.ErrorCount, actual.ErrorCount);
        Assert.AreEqual(record.ErrorRatePercent, actual.ErrorRatePercent);
        Assert.AreEqual(record.ErrorThresholdPercent, actual.ErrorThresholdPercent);
        Assert.AreEqual(record.NoiseType, actual.NoiseType);
        Assert.AreEqual(record.NoiseLevelDb, actual.NoiseLevelDb);
        Assert.AreEqual(record.NoiseBandwidthHz, actual.NoiseBandwidthHz);
        Assert.AreEqual(record.AgcEnabled, actual.AgcEnabled);
        Assert.AreEqual(record.AgcDelaySeconds, actual.AgcDelaySeconds);
        Assert.AreEqual(record.AgcMaxGainDb, actual.AgcMaxGainDb);
        Assert.AreEqual(record.ApfEnabled, actual.ApfEnabled);
        Assert.AreEqual(record.ApfBandwidthHz, actual.ApfBandwidthHz);
        Assert.AreEqual(record.ApfPeakGainDb, actual.ApfPeakGainDb);

        var confusions = await sut.GetConfusionObservationsAsync();
        Assert.HasCount(1, confusions);
        Assert.AreEqual("A", confusions[0].ExpectedSymbol);
        Assert.AreEqual("B", confusions[0].ActualSymbol);
        Assert.AreEqual(2, confusions[0].Count);
    }

    [TestMethod]
    public async Task GetStatisticsRecordsAsync_MigratesLegacyDatabase()
    {
        var databasePath = Path.Combine(_tempDirectory, "practice-results.db");
        Directory.CreateDirectory(_tempDirectory);
        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE practice_result_statistics (
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
                INSERT INTO practice_result_statistics VALUES
                    (1, '1970-01-01T00:00:00.0000000+00:00', 20, 15, 10, 1, 10.0,
                     'None', -15.0, 500.0, 1, 0.4, 1, 120.0, -9.0);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());
        var records = await sut.GetStatisticsRecordsAsync();

        Assert.HasCount(1, records);
        Assert.AreEqual(0.0, records[0].ErrorThresholdPercent);
        // The v3 migration backfills the AGC max-gain column with the old hardcoded value.
        Assert.AreEqual(18.0, records[0].AgcMaxGainDb);
        Assert.AreEqual(3L, await ScalarAsync(databasePath, "SELECT version FROM schema_info;"));
    }

    [TestMethod]
    public async Task GetStatisticsRecordsAsync_InvalidTimestampThrowsStatisticsStoreException()
    {
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());
        await sut.SaveAsync(CreateRecord());

        await using (var connection = new SqliteConnection($"Data Source={sut.DatabasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE practice_result_statistics SET recorded_at = 'not-a-timestamp';";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsExactlyAsync<StatisticsStoreException>(
            () => sut.GetStatisticsRecordsAsync());
    }

    [TestMethod]
    public async Task ConcurrentSavesAndReads_DoNotLockTheDatabase()
    {
        // WAL + the per-instance operation gate must let interleaved saves and reads complete
        // without surfacing "database is locked" to the caller.
        var sut = new PracticeResultStatisticsStore(FakePaths(_tempDirectory), Logger());

        var saveTasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => sut.SaveAsync(CreateRecord())));
        var readTasks = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => sut.GetStatisticsRecordsAsync()));

        await Task.WhenAll(saveTasks.Concat(readTasks));

        var records = await sut.GetStatisticsRecordsAsync();
        Assert.HasCount(8, records);
    }

    private static IAppPaths FakePaths(string directory)
    {
        var paths = Substitute.For<IAppPaths>();
        paths.AppDataDirectory.Returns(directory);
        paths.UserConfigPaths.Returns(new List<string>());
        paths.PreferredUserConfigPath.Returns((string?)null);
        return paths;
    }

    private static ILogger<PracticeResultStatisticsStore> Logger()
        => Substitute.For<ILogger<PracticeResultStatisticsStore>>();

    private static PracticeResultStatisticsRecord CreateRecord() => new()
    {
        RecordedAt = DateTimeOffset.UnixEpoch,
        CharacterWpm = 20,
        AverageWpm = 15,
        CharacterCount = 10,
        ErrorCount = 1,
        ErrorRatePercent = 10.0,
        NoiseType = NoiseType.None,
        NoiseLevelDb = -15.0,
        NoiseBandwidthHz = 500.0,
        AgcEnabled = true,
        AgcDelaySeconds = 0.4,
        AgcMaxGainDb = 18.0,
        ApfEnabled = true,
        ApfBandwidthHz = 120.0,
        ApfPeakGainDb = -9.0,
    };

    private static async Task<long> CountRowsAsync(string databasePath)
    {
        return await ScalarAsync(databasePath, "SELECT COUNT(*) FROM practice_result_statistics;");
    }

    private static async Task<long> ScalarAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
