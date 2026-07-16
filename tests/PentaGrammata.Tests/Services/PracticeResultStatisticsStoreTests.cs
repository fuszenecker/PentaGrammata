using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

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
        ApfEnabled = true,
        ApfBandwidthHz = 120.0,
        ApfPeakGainDb = -9.0,
    };

    private static async Task<long> CountRowsAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM practice_result_statistics;";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
