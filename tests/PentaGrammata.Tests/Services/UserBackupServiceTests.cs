using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;
using PentaGrammata.Stores;
using Promptuarium;
using Promptuarium.Serializer;

namespace PentaGrammata.Tests.Services;

// These tests drive the real store/service stack over a temporary IAppPaths so the
// archive round-trip, including the live reloads, is exercised end to end.
[TestClass]
public sealed class UserBackupServiceTests
{
    private static byte[] EmptyContainer() => PromptuariumSerializer.Serialize(new Dictionary<string, string>(), ConfigurationFormat.Options);

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
    public async Task ExportAsync_ProducesPpakArchiveWithSettingsAndDatabaseOnly()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        context.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await context.ConfigService.SaveAsync();

        using var buffer = new MemoryStream();
        await context.Backup.ExportAsync(buffer);

        // The layout of Promptuarium's ppak: entries named by their data, each with a "type",
        // a "content" and a "crc32" info node.
        var root = Element.FromBytes(buffer.ToArray());
        Assert.AreEqual("PRMT:PPAK1-LE", root.MetaData.AsAsciiString());
        CollectionAssert.AreEqual(
            new[] { "settings.prmt", "practice-results.db" },
            root.Children.Select(entry => entry.Data.AsUtf8String()).ToArray());
        foreach (var entry in root.Children)
        {
            var flags = entry.Children.Select(info => info.MetaData.AsAsciiString()).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "type", "content", "crc32" }, flags);
        }
    }

    [TestMethod]
    public async Task ExportAsync_DatabaseEntryIsAReadableSnapshotWithSavedData()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        await context.Statistics.SaveAsync(CreateRecord());

        var archiveBytes = await ExportToBytesAsync(context.Backup);
        var databasePath = Path.Combine(_tempDirectory, "exported.db");
        File.WriteAllBytes(databasePath, ReadEntry(archiveBytes, "practice-results.db"));

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

        var archiveBytes = await ExportToBytesAsync(context.Backup);
        var settings = PromptuariumSerializer.Deserialize<AppConfig>(ReadEntry(archiveBytes, "settings.prmt"), ConfigurationFormat.Options);

        Assert.IsNotNull(settings);
        Assert.AreEqual(33, settings.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task ImportAsync_RoundTrip_ReplacesDatabaseAndConfigButNotWindowSizes()
    {
        var source = CreateContext(Path.Combine(_tempDirectory, "source"));
        await source.Statistics.SaveAsync(CreateRecord());
        source.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        source.ConfigService.Current.Practice.CharacterWpm = 44;
        await source.ConfigService.SaveAsync();

        var archiveBytes = await ExportToBytesAsync(source.Backup);

        var target = CreateContext(Path.Combine(_tempDirectory, "target"));
        // Diverge the target: two records and a different window size.
        await target.Statistics.SaveAsync(CreateRecord());
        await target.Statistics.SaveAsync(CreateRecord());
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);
        var targetCurrent = target.ConfigService.Current;
        targetCurrent.Practice.CharacterWpm = 22;

        var result = await target.Backup.ImportAsync(new MemoryStream(archiveBytes));
        var contents = result.Contents;

        Assert.IsTrue(contents.AppSettings);
        Assert.IsTrue(contents.Database);

        // The live configuration was reloaded into the same shared instance.
        Assert.AreSame(targetCurrent, target.ConfigService.Current);
        Assert.AreEqual(44, target.ConfigService.Current.Practice.CharacterWpm);

        // The statistics database was replaced with the source's single record.
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());

        // Window sizes are not part of a backup: the target keeps its own.
        Assert.AreEqual((100.0, 100.0), target.WindowSizes.TryGetSize("TrendsDialog")!.Value);

        // The automatic backup contains the original data, including unsaved live settings.
        Assert.IsTrue(Path.IsPathFullyQualified(result.BackupPath));
        Assert.AreEqual(Path.Combine(_tempDirectory, "target", "backups"), Path.GetDirectoryName(result.BackupPath));
        var backupBytes = await File.ReadAllBytesAsync(result.BackupPath);
        var backedUpSettings = PromptuariumSerializer.Deserialize<AppConfig>(ReadEntry(backupBytes, "settings.prmt"), ConfigurationFormat.Options);
        Assert.IsNotNull(backedUpSettings);
        Assert.AreEqual(22, backedUpSettings.Practice.CharacterWpm);

        var restored = CreateContext(Path.Combine(_tempDirectory, "restored"));
        await restored.Backup.ImportAsync(new MemoryStream(backupBytes));
        Assert.AreEqual(22, restored.ConfigService.Current.Practice.CharacterWpm);
        Assert.HasCount(2, await restored.Statistics.GetStatisticsRecordsAsync());
        Assert.IsNull(restored.WindowSizes.TryGetSize("TrendsDialog"));
        Assert.AreEqual(IUserBackupService.FileExtension, Path.GetExtension(result.BackupPath));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(result.BackupPath)!, "*.tmp"));
    }

    [TestMethod]
    public async Task ImportAsync_WithPartialArchive_ImportsOnlyPresentEntries()
    {
        var target = CreateContext(_tempDirectory);
        await target.Statistics.SaveAsync(CreateRecord());

        var settings = PromptuariumSerializer.Serialize(new AppConfig
        {
            Practice = new Practice { CharacterWpm = 66 },
            CharacterSets = new CharacterSets { ["Letters"] = "ABCDEF" },
        }, ConfigurationFormat.Options);
        var archiveBytes = CreateArchive(("settings.prmt", settings));

        var result = await target.Backup.ImportAsync(new MemoryStream(archiveBytes));
        var contents = result.Contents;

        Assert.IsTrue(contents.AppSettings);
        Assert.IsFalse(contents.Database);
        Assert.AreEqual(66, target.ConfigService.Current.Practice.CharacterWpm);
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());
    }

    [TestMethod]
    public async Task ImportAsync_RepeatedImports_KeepSeparateBackups()
    {
        var target = CreateContext(_tempDirectory);
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        var settings = PromptuariumSerializer.Serialize(new AppConfig
        {
            Practice = new Practice { CharacterWpm = 44 },
        }, ConfigurationFormat.Options);
        var archiveBytes = CreateArchive(("settings.prmt", settings));

        var first = await target.Backup.ImportAsync(new MemoryStream(archiveBytes));
        var firstBytes = await File.ReadAllBytesAsync(first.BackupPath);
        var second = await target.Backup.ImportAsync(new MemoryStream(archiveBytes));

        Assert.AreNotEqual(first.BackupPath, second.BackupPath);
        CollectionAssert.AreEqual(firstBytes, await File.ReadAllBytesAsync(first.BackupPath));
        Assert.AreEqual(22, PromptuariumSerializer.Deserialize<AppConfig>(ReadEntry(firstBytes, "settings.prmt"), ConfigurationFormat.Options)!.Practice.CharacterWpm);
        var secondBytes = await File.ReadAllBytesAsync(second.BackupPath);
        Assert.AreEqual(44, PromptuariumSerializer.Deserialize<AppConfig>(ReadEntry(secondBytes, "settings.prmt"), ConfigurationFormat.Options)!.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task RestoreLatestAsync_RestoresPreImportStateAndKeepsBeforeRestoreArchive()
    {
        var target = CreateContext(Path.Combine(_tempDirectory, "target"));
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        await target.ConfigService.SaveAsync();
        await target.Statistics.SaveAsync(CreateRecord());
        await target.Statistics.SaveAsync(CreateRecord());
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);

        var source = CreateContext(Path.Combine(_tempDirectory, "source"));
        source.ConfigService.Current.Practice.CharacterWpm = 44;
        await source.ConfigService.SaveAsync();
        await source.Statistics.SaveAsync(CreateRecord());
        source.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await target.Backup.ImportAsync(new MemoryStream(await ExportToBytesAsync(source.Backup)));

        var restored = await target.Backup.RestoreLatestAsync();

        Assert.AreEqual(22, target.ConfigService.Current.Practice.CharacterWpm);
        Assert.HasCount(2, await target.Statistics.GetStatisticsRecordsAsync());
        Assert.AreEqual((100.0, 100.0), target.WindowSizes.TryGetSize("TrendsDialog")!.Value);
        Assert.IsTrue(File.Exists(restored.BackupPath));
        StringAssert.Contains(Path.GetFileName(restored.BackupPath), "before-restore");
    }

    [TestMethod]
    public async Task RestoreLatestAsync_WhenNoRestorePointExists_ThrowsHelpfulError()
    {
        var target = CreateContext(_tempDirectory);

        var exception = await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.RestoreLatestAsync());

        StringAssert.Contains(exception.Message, "No pre-import restore point");
    }

    [TestMethod]
    public async Task ImportAsync_WhenBackupDirectoryCannotBeCreated_KeepsAllLiveData()
    {
        var source = CreateContext(Path.Combine(_tempDirectory, "source"));
        source.ConfigService.Current.Practice.CharacterWpm = 44;
        source.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await source.Statistics.SaveAsync(CreateRecord());
        var archiveBytes = await ExportToBytesAsync(source.Backup);

        var targetDirectory = Path.Combine(_tempDirectory, "target");
        var target = CreateContext(targetDirectory);
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        await target.ConfigService.SaveAsync();
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);
        await target.Statistics.SaveAsync(CreateRecord());
        await target.Statistics.SaveAsync(CreateRecord());
        var settingsBytes = await File.ReadAllBytesAsync(Path.Combine(targetDirectory, "settings.prmt"));
        File.WriteAllText(Path.Combine(targetDirectory, "backups"), "blocks directory creation");

        var exception = await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));

        StringAssert.Contains(exception.Message, "No data was replaced");
        Assert.IsNull(exception.BackupPath);
        Assert.AreEqual(22, target.ConfigService.Current.Practice.CharacterWpm);
        CollectionAssert.AreEqual(settingsBytes, await File.ReadAllBytesAsync(Path.Combine(targetDirectory, "settings.prmt")));
        Assert.HasCount(2, await target.Statistics.GetStatisticsRecordsAsync());
        Assert.AreEqual((100.0, 100.0), target.WindowSizes.TryGetSize("TrendsDialog")!.Value);
    }

    [TestMethod]
    public async Task ImportAsync_WhenBackupSnapshotFails_DoesNotReplaceDataOrLeaveIncompleteArchive()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.AppDataDirectory.Returns(_tempDirectory);
        var configuration = Substitute.For<IConfigurationService>();
        configuration.Current.Returns(new AppConfig());
        var statistics = Substitute.For<IPracticeResultStatisticsStore>();
        statistics.CreateConsistentCopyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new StatisticsStoreException("Snapshot failed.", new IOException("Disk full."))));
        var backup = new UserBackupService(paths, configuration, statistics,
            Substitute.For<ILogger<UserBackupService>>());
        var archiveBytes = CreateArchive(("settings.prmt", EmptyContainer()));

        var exception = await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => backup.ImportAsync(new MemoryStream(archiveBytes)));

        StringAssert.Contains(exception.Message, "No data was replaced");
        Assert.IsNull(exception.BackupPath);
        Assert.IsEmpty(Directory.GetFiles(Path.Combine(_tempDirectory, "backups")));
        await statistics.DidNotReceive().ReplaceDatabaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        configuration.DidNotReceive().ReloadFromDisk();
        configuration.DidNotReceive().ApplyImported(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public async Task ImportAsync_WhenReplacementFails_RollsBackAndReportsRecoveryPath()
    {
        var source = CreateContext(Path.Combine(_tempDirectory, "source"));
        await source.Statistics.SaveAsync(CreateRecord());
        source.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        var archiveBytes = await ExportToBytesAsync(source.Backup);

        var targetDirectory = Path.Combine(_tempDirectory, "target");
        var target = CreateContext(targetDirectory);
        await target.Statistics.SaveAsync(CreateRecord());
        await target.Statistics.SaveAsync(CreateRecord());
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        // Fail settings replacement after the database was replaced.
        Directory.CreateDirectory(Path.Combine(targetDirectory, "settings.prmt"));

        var exception = await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));

        Assert.IsNotNull(exception.BackupPath);
        Assert.IsTrue(File.Exists(exception.BackupPath));
        var backupBytes = await File.ReadAllBytesAsync(exception.BackupPath);
        var databasePath = Path.Combine(_tempDirectory, "original.db");
        await File.WriteAllBytesAsync(databasePath, ReadEntry(backupBytes, "practice-results.db"));
        Assert.AreEqual(2L, await CountRowsAsync(databasePath));
        Assert.AreEqual(22, target.ConfigService.Current.Practice.CharacterWpm);
        Assert.HasCount(2, await target.Statistics.GetStatisticsRecordsAsync());
        Assert.AreEqual((100.0, 100.0), target.WindowSizes.TryGetSize("TrendsDialog")!.Value);
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

        // SqliteConnection pooling may retain the legacy file handle after disposal.
        SqliteConnection.ClearAllPools();
        var archiveBytes = CreateArchive(("practice-results.db", await File.ReadAllBytesAsync(legacyPath)));

        await target.Backup.ImportAsync(new MemoryStream(archiveBytes));

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
        var archiveBytes = CreateArchive(("readme.txt", "not a backup"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));
    }

    [TestMethod]
    public async Task ImportAsync_WhenNotAnArchive_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream("plain text, not an archive"u8.ToArray())));
    }

    [TestMethod]
    public async Task ImportAsync_WhenZipArchive_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);
        // The start of a ZIP: the old backup format is not read any more.
        var archiveBytes = "PK\u0003\u0004\u0014\u0000\u0000\u0000settings.prmt"u8.ToArray();

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));
    }

    [TestMethod]
    public async Task ImportAsync_OnlyNameAndContent_IsEnough()
    {
        var target = CreateContext(_tempDirectory);
        var settings = PromptuariumSerializer.Serialize(new AppConfig { Practice = new Practice { CharacterWpm = 44 } }, ConfigurationFormat.Options);
        // No type, no CRC, no time, an unknown info node, and a repeated name (the first one counts).
        var root = new ElementBuilder().WithSignature(Data.FromAsciiString("PRMT:PPAK1-LE")).Build();
        root.Add(new Element(Data.FromUtf8String("SETTINGS.PRMT"), null,
            new Element(Data.FromUtf8String("whatever"), Data.FromAsciiString("note")),
            new Element(settings, Data.FromAsciiString("content"))));
        root.Add(new Element(Data.FromUtf8String("settings.prmt"), null,
            new Element("not a container"u8.ToArray(), Data.FromAsciiString("content"))));

        var result = await target.Backup.ImportAsync(new MemoryStream(root.ToBytes()));

        Assert.AreEqual(new BackupContents(AppSettings: true, Database: false), result.Contents);
        Assert.AreEqual(44, target.ConfigService.Current.Practice.CharacterWpm);
    }

    [TestMethod]
    [DataRow("PRMT:PPAK1")]
    [DataRow("PRMT:PPak1-LE")]
    [DataRow("prmt:ppak1-le")]
    [DataRow("PRMT:OTHER")]
    [DataRow(null)]
    public async Task ImportAsync_WhenSignatureIsNotExact_ThrowsAndKeepsLiveData(string? signature)
    {
        var target = CreateContext(_tempDirectory);
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        var settings = PromptuariumSerializer.Serialize(new AppConfig { Practice = new Practice { CharacterWpm = 44 } }, ConfigurationFormat.Options);
        var root = Element.FromBytes(CreateArchive(("settings.prmt", settings)));
        root.MetaData = signature is null ? ReadOnlyMemory<byte>.Empty : Data.FromAsciiString(signature);

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(root.ToBytes())));

        Assert.AreEqual(22, target.ConfigService.Current.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task ImportAsync_WhenContentDoesNotMatchCrc_ThrowsAndKeepsLiveData()
    {
        var target = CreateContext(_tempDirectory);
        target.ConfigService.Current.Practice.CharacterWpm = 22;
        var settings = PromptuariumSerializer.Serialize(new AppConfig { Practice = new Practice { CharacterWpm = 44 } }, ConfigurationFormat.Options);
        var root = Element.FromBytes(CreateArchive(("settings.prmt", settings)));
        root.Children[0].Children.Single(info => info.MetaData.AsAsciiString() == "crc32").Data = Data.FromUInt(0);

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(root.ToBytes())));

        Assert.AreEqual(22, target.ConfigService.Current.Practice.CharacterWpm);
        Assert.IsFalse(Directory.Exists(Path.Combine(_tempDirectory, "backups")));
    }

    [TestMethod]
    public async Task ImportAsync_IgnoresWindowSizesAndPathTraversalEntries()
    {
        var target = CreateContext(_tempDirectory);
        target.WindowSizes.SaveSize("TrendsDialog", 100, 100);
        var windowSizesPath = Path.Combine(_tempDirectory, "windows.prmt");
        var windowSizesBytes = await File.ReadAllBytesAsync(windowSizesPath);
        var settings = PromptuariumSerializer.Serialize(new AppConfig { Practice = new Practice { CharacterWpm = 44 } }, ConfigurationFormat.Options);
        var archive = CreateArchive(("windows.prmt", EmptyContainer()), ("../settings.prmt", EmptyContainer()), ("settings.prmt", settings));

        var result = await target.Backup.ImportAsync(new MemoryStream(archive));

        Assert.AreEqual(new BackupContents(AppSettings: true, Database: false), result.Contents);
        Assert.AreEqual(44, target.ConfigService.Current.Practice.CharacterWpm);
        CollectionAssert.AreEqual(windowSizesBytes, await File.ReadAllBytesAsync(windowSizesPath));
        Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(_tempDirectory)!, "settings.prmt")));
    }

    [TestMethod]
    public async Task ImportAsync_WhenDatabaseEntryIsNotSqlite_ThrowsAndKeepsLiveData()
    {
        var target = CreateContext(_tempDirectory);
        await target.Statistics.SaveAsync(CreateRecord());

        var archiveBytes = CreateArchive(("practice-results.db", "garbage bytes"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));

        // Validation runs before any replacement, so the live database survives.
        Assert.HasCount(1, await target.Statistics.GetStatisticsRecordsAsync());
        Assert.IsFalse(Directory.Exists(Path.Combine(_tempDirectory, "backups")));
    }

    [TestMethod]
    public async Task ImportAsync_WhenSettingsEntryIsNotAContainer_ThrowsUserBackupException()
    {
        var target = CreateContext(_tempDirectory);
        var archiveBytes = CreateArchive(("settings.prmt", "{ this is not a container"u8.ToArray()));

        await Assert.ThrowsExactlyAsync<UserBackupException>(
            () => target.Backup.ImportAsync(new MemoryStream(archiveBytes)));
    }

    [TestMethod]
    public async Task ImportAsync_WhenPreferredConfigPathIsNull_AppliesConfigInMemoryOnly()
    {
        var target = CreateContext(_tempDirectory, withUserConfigPath: false);
        var settings = PromptuariumSerializer.Serialize(new AppConfig
        {
            Practice = new Practice { CharacterWpm = 55 },
            CharacterSets = new CharacterSets { ["Letters"] = "ABCDEF" },
        }, ConfigurationFormat.Options);
        var archiveBytes = CreateArchive(("settings.prmt", settings));

        var result = await target.Backup.ImportAsync(new MemoryStream(archiveBytes));

        Assert.IsTrue(result.Contents.AppSettings);
        Assert.AreEqual(55, target.ConfigService.Current.Practice.CharacterWpm);
        // No per-user file exists on this platform, so nothing may have been written.
        Assert.IsFalse(File.Exists(Path.Combine(_tempDirectory, "settings.prmt")));
        var backupBytes = await File.ReadAllBytesAsync(result.BackupPath);
        Assert.IsNotNull(PromptuariumSerializer.Deserialize<AppConfig>(ReadEntry(backupBytes, "settings.prmt"), ConfigurationFormat.Options));
    }

    [TestMethod]
    public async Task InspectArchive_ReturnsContentsOfValidArchive()
    {
        var context = CreateContext(_tempDirectory);
        await context.Statistics.SaveAsync(CreateRecord());
        context.WindowSizes.SaveSize("TrendsDialog", 980, 600);
        await context.ConfigService.SaveAsync();

        var archiveBytes = await ExportToBytesAsync(context.Backup);

        var contents = context.Backup.InspectArchive(new MemoryStream(archiveBytes));

        Assert.IsNotNull(contents);
        Assert.IsTrue(contents.AppSettings);
        Assert.IsTrue(contents.Database);
    }

    [TestMethod]
    public void InspectArchive_WhenNotAnArchive_ReturnsNull()
    {
        var context = CreateContext(_tempDirectory);

        var contents = context.Backup.InspectArchive(new MemoryStream("not an archive"u8.ToArray()));

        Assert.IsNull(contents);
    }

    [TestMethod]
    public void InspectArchive_WhenArchiveHasNoKnownEntries_ReturnsNull()
    {
        var context = CreateContext(_tempDirectory);
        var archiveBytes = CreateArchive(("unrelated.txt", "hello"u8.ToArray()));

        var contents = context.Backup.InspectArchive(new MemoryStream(archiveBytes));

        Assert.IsNull(contents);
    }

    private static async Task<byte[]> ExportToBytesAsync(UserBackupService backup)
    {
        using var buffer = new MemoryStream();
        await backup.ExportAsync(buffer);
        return buffer.ToArray();
    }

    private static byte[] CreateArchive(params (string EntryName, byte[] Bytes)[] entries)
    {
        using var buffer = new MemoryStream();
        BackupArchive.Write(buffer, entries);
        return buffer.ToArray();
    }

    private static byte[] ReadEntry(byte[] archiveBytes, string entryName)
    {
        return BackupArchive.Read(new MemoryStream(archiveBytes), [entryName])[entryName];
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
    /// is substituted with a settings.prmt-backed load/save (the real one layers the bundled
    /// defaults from the application directory, which do not exist for tests),
    /// which still exercises the actual file bytes on the reload-from-disk path.
    /// </summary>
    private static BackupContext CreateContext(string directory, bool withUserConfigPath = true)
    {
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "settings.prmt");

        var paths = Substitute.For<IAppPaths>();
        paths.AppDataDirectory.Returns(directory);
        paths.UserConfigPaths.Returns(withUserConfigPath ? new List<string> { configPath } : new List<string>());
        paths.PreferredUserConfigPath.Returns(withUserConfigPath ? configPath : (string?)null);

        var configStore = Substitute.For<IConfigurationStore>();
        configStore.Load().Returns(_ => LoadConfig(configPath));
        configStore
            .SaveAsync(Arg.Do<AppConfig>(config => File.WriteAllBytes(configPath, PromptuariumSerializer.Serialize(config, ConfigurationFormat.Options))))
            .Returns(Task.CompletedTask);

        var configService = new ConfigurationService(configStore, Substitute.For<ILogger<ConfigurationService>>());
        var statistics = new PracticeResultStatisticsStore(paths, Substitute.For<ILogger<PracticeResultStatisticsStore>>());
        var windowSizes = new WindowSizeStore(paths, Substitute.For<ILogger<WindowSizeStore>>());
        var backup = new UserBackupService(
            paths,
            configService,
            statistics,
            Substitute.For<ILogger<UserBackupService>>());

        return new BackupContext(backup, configService, statistics, windowSizes);
    }

    private static AppConfig LoadConfig(string path)
    {
        return File.Exists(path)
            ? PromptuariumSerializer.Deserialize<AppConfig>(File.ReadAllBytes(path), ConfigurationFormat.Options)
            : new AppConfig();
    }

    private sealed record BackupContext(
        UserBackupService Backup,
        ConfigurationService ConfigService,
        PracticeResultStatisticsStore Statistics,
        WindowSizeStore WindowSizes);
}
