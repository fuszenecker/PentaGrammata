using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;
using PentaGrammata.Stores;

namespace PentaGrammata.Tests.Services;

// These tests drive the real store/service stack over a temporary IAppPaths so the
// archive round-trip, including the live reloads, is exercised end to end.
[TestClass]
public sealed class UserBackupServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string _tempDirectory = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PentaGrammataBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void TearDown()
    {
        // The stores open pooled SQLite connections; release the file handles the pool
        // holds before deleting, otherwise the directory delete fails on Windows.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExportAsync_ProducesArchiveWithAllThreeEntries()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        context.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await context.ConfigService.SaveAsync();

        using var buffer = new MemoryStream();
        await context.Backup.ExportAsync(buffer);

        using var archive = new ZipArchive(new MemoryStream(buffer.ToArray()), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(new[] { "appsettings.json", "practice-results.db", "window-sizes.json" }, names);
    }

    [TestMethod]
    public async Task ExportAsync_DatabaseEntryIsAReadableSnapshotWithSavedData()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        await context.Statistics.SaveAsync(CreateRecord());

        var zip = await ExportToBytesAsync(context.Backup);
        var databasePath = Path.Combine(_tempDirectory, "exported.db");
        File.WriteAllBytes(databasePath, ReadEntry(zip, "practice-results.db"));

        // The snapshot must be readable on its own, even though the live database keeps
        // its committed data in WAL sidecars.
        Assert.AreEqual(2L, await CountRowsAsync(databasePath));
        Assert.IsFalse(File.Exists(databasePath + "-wal"));
    }

    [TestMethod]
    public async Task ExportAsync_IncludesLiveConfiguration()
    {
        var context = CreateContext(_tempDirectory);
        context.ConfigService.Current.Practice.CharacterWpm = 33;
        await context.ConfigService.SaveAsync();

        var zip = await ExportToBytesAsync(context.Backup);
        var settings = JsonSerializer.Deserialize<AppConfig>(ReadEntry(zip, "appsettings.json"));

        Assert.IsNotNull(settings);
        Assert.AreEqual(33, settings.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task ExportAsync_WhenWindowSizesFileMissing_OmitsEntry()
    {
        var context = CreateContext(_tempDirectory);

        var zip = await ExportToBytesAsync(context.Backup);

        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.IsNull(archive.GetEntry("window-sizes.json"));
        Assert.IsNotNull(archive.GetEntry("appsettings.json"));
    }

    [TestMethod]
    public async Task ImportAsync_RoundTrip_ReplacesDatabaseConfigAndWindowSizes()
    {
        var source = CreateContext(Path.Combine(_tempDirectory, "source"));
        await source.Statistics.SaveAsync(CreateRecord());
        source.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        source.ConfigService.Current.Practice.CharacterWpm = 44;
        await source.ConfigService.SaveAsync();

        var zip = await ExportToBytesAsync(source.Backup);

        var target = CreateContext(Path.Combine(_tempDirectory, "target"));
        // Diverge the target: two records, a different window size, and a warm window-size
        // cache that would otherwise hide the imported file behind stale data.
        await target.Statistics.SaveAsync(CreateRecord());
        await target.Statistics.SaveAsync(CreateRecord());
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);
        var targetCurrent = target.ConfigService.Current;

        var contents = await target.Backup.ImportAsync(new MemoryStream(zip));

        Assert.IsTrue(contents.AppSettings);
        Assert.IsTrue(contents.Database);
        Assert.IsTrue(contents.WindowSizes);

        // The live configuration was reloaded into the same shared instance.
        Assert.AreSame(targetCurrent, target.ConfigService.Current);
        Assert.AreEqual(44, target.ConfigService.Current.Practice.CharacterWpm);

        // The statistics database was replaced with the source's single record.
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());

        // The window-size cache was invalidated, so the imported size is visible.
        var size = target.WindowSizes.TryGetSize("TrendsDialog");
        Assert.IsNotNull(size);
        Assert.AreEqual(980, size.Value.Width);
        Assert.AreEqual(600, size.Value.Height);
    }

    [TestMethod]
    public async Task ImportAsync_WithPartialArchive_ImportsOnlyPresentEntries()
    {
        var target = CreateContext(_tempDirectory);
        await target.Statistics.SaveAsync(CreateRecord());

        var settings = JsonSerializer.Serialize(new AppConfig
        {
            Practice = new Practice { CharacterWpm = 66 },
            CharacterSets = new CharacterSets { ["Letters"] = "ABCDEF" },
        }, JsonOptions);
        var zip = CreateZip(("appsettings.json", Encoding.UTF8.GetBytes(settings)));

        var contents = await target.Backup.ImportAsync(new MemoryStream(zip));

        Assert.IsTrue(contents.AppSettings);
        Assert.IsFalse(contents.Database);
        Assert.IsFalse(contents.WindowSizes);
        Assert.AreEqual(66, target.ConfigService.Current.Practice.CharacterWpm);
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());
    }

    [TestMethod]
    public async Task ImportAsync_WithOlderSchemaDatabase_MigratesAndReads()
    {
        var target = CreateContext(_tempDirectory);
        await target.Statistics.SaveAsync(CreateRecord());

        // A pre-v2 database without schema_info, error_threshold_percent or QSB columns.
        var legacyPath = Path.Combine(_tempDirectory, "legacy.db");
        await using (var connection = new SqliteConnection($"Data Source={legacyPath}"))
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

        var zip = CreateZip(("practice-results.db", await File.ReadAllBytesAsync(legacyPath)));

        await target.Backup.ImportAsync(new MemoryStream(zip));

        var records = await target.Statistics.GetStatisticsRecordsAsync();
        Assert.HasCount(1, records);
        Assert.AreEqual(0.0, records[0].ErrorThresholdPercent);
        Assert.AreEqual(18.0, records[0].AgcMaxGainDb);
        Assert.IsFalse(records[0].QsbEnabled);
    }

    [TestMethod]
    public async Task ImportAsync_WhenNoKnownEntries_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);
        var zip = CreateZip(("readme.txt", "not a backup"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(zip)));
    }

    [TestMethod]
    public async Task ImportAsync_WhenNotAZip_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream("plain text, not a zip"u8.ToArray())));
    }

    [TestMethod]
    public async Task ImportAsync_WhenDatabaseEntryIsNotSqlite_ThrowsAndKeepsLiveData()
    {
        var target = CreateContext(_tempDirectory);
        await target.Statistics.SaveAsync(CreateRecord());

        var zip = CreateZip(("practice-results.db", "garbage bytes"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(zip)));

        // Validation runs before any replacement, so the live database survives.
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());
    }

    [TestMethod]
    public async Task ImportAsync_WhenWindowSizesEntryIsNotAnObject_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);
        var zip = CreateZip(("window-sizes.json", "[1,2,3]"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(zip)));
    }

    [TestMethod]
    public async Task ImportAsync_WhenSettingsEntryIsNotJson_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);
        var zip = CreateZip(("appsettings.json", "{ this is not json"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(zip)));
    }

    [TestMethod]
    public async Task ImportAsync_WhenPreferredConfigPathIsNull_AppliesConfigInMemoryOnly()
    {
        var target = CreateContext(_tempDirectory, withUserConfigPath: false);
        var settings = JsonSerializer.Serialize(new AppConfig
        {
            Practice = new Practice { CharacterWpm = 55 },
            CharacterSets = new CharacterSets { ["Letters"] = "ABCDEF" },
        }, JsonOptions);
        var zip = CreateZip(("appsettings.json", Encoding.UTF8.GetBytes(settings)));

        var contents = await target.Backup.ImportAsync(new MemoryStream(zip));

        Assert.IsTrue(contents.AppSettings);
        Assert.AreEqual(55, target.ConfigService.Current.Practice.CharacterWpm);
        // No per-user file exists on this platform, so nothing may have been written.
        Assert.IsFalse(File.Exists(Path.Combine(_tempDirectory, "appsettings.json")));
    }

    [TestMethod]
    public async Task InspectArchive_ReturnsContentsOfValidArchive()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        context.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await context.ConfigService.SaveAsync();

        var zip = await ExportToBytesAsync(context.Backup);

        var contents = context.Backup.InspectArchive(new MemoryStream(zip));

        Assert.IsNotNull(contents);
        Assert.IsTrue(contents.AppSettings);
        Assert.IsTrue(contents.Database);
        Assert.IsTrue(contents.WindowSizes);
    }

    [TestMethod]
    public void InspectArchive_WhenNotAZip_ReturnsNull()
    {
        var context = CreateContext(_tempDirectory);

        var contents = context.Backup.InspectArchive(new MemoryStream("not a zip"u8.ToArray()));

        Assert.IsNull(contents);
    }

    [TestMethod]
    public void InspectArchive_WhenArchiveHasNoKnownEntries_ReturnsNull()
    {
        var context = CreateContext(_tempDirectory);
        var zip = CreateZip(("unrelated.txt", "hello"u8.ToArray()));

        var contents = context.Backup.InspectArchive(new MemoryStream(zip));

        Assert.IsNull(contents);
    }

    private static async Task<byte[]> ExportToBytesAsync(UserBackupService backup)
    {
        using var buffer = new MemoryStream();
        await backup.ExportAsync(buffer);
        return buffer.ToArray();
    }

    private static byte[] CreateZip(params (string EntryName, byte[] Bytes)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create))
        {
            foreach (var (entryName, bytes) in entries)
            {
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        return buffer.ToArray();
    }

    private static byte[] ReadEntry(byte[] zipBytes, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        using var entryStream = archive.GetEntry(entryName)!.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static async Task<long> CountRowsAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM practice_result_statistics;";
        return (long)(await command.ExecuteScalarAsync())!;
    }

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

    /// <summary>
    /// Builds the real service stack over a temporary directory. The configuration store
    /// is substituted with a JSON-file-backed load/save (the real one layers the bundled
    /// appsettings.json from the application directory, which does not exist for tests),
    /// which still exercises the actual file bytes on the reload-from-disk path.
    /// </summary>
    private static BackupContext CreateContext(string directory, bool withUserConfigPath = true)
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "appsettings.json");

        var paths = Substitute.For<IAppPaths>();
        paths.AppDataDirectory.Returns(directory);
        paths.UserConfigPaths.Returns(withUserConfigPath ? new List<string> { configPath } : new List<string>());
        paths.PreferredUserConfigPath.Returns(withUserConfigPath ? configPath : (string?)null);

        var configStore = Substitute.For<IConfigurationStore>();
        configStore.Load().Returns(_ => LoadConfig(configPath));
        configStore
            .SaveAsync(Arg.Do<AppConfig>(config => File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions))))
            .Returns(Task.CompletedTask);

        var configService = new ConfigurationService(configStore, Substitute.For<ILogger<ConfigurationService>>());
        var statistics = new PracticeResultStatisticsStore(paths, Substitute.For<ILogger<PracticeResultStatisticsStore>>());
        var windowSizes = new WindowSizeStore(paths, Substitute.For<ILogger<WindowSizeStore>>());
        var backup = new UserBackupService(
            paths,
            configService,
            statistics,
            windowSizes,
            Substitute.For<ILogger<UserBackupService>>());

        return new BackupContext(backup, configService, statistics, windowSizes);
    }

    private static AppConfig LoadConfig(string path)
    {
        return File.Exists(path)
            ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? new AppConfig()
            : new AppConfig();
    }

    private sealed record BackupContext(
        UserBackupService Backup,
        ConfigurationService ConfigService,
        PracticeResultStatisticsStore Statistics,
        WindowSizeStore WindowSizes);
}
