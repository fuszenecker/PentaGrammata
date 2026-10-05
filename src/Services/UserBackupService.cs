using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Archives and restores the user's data files (per-user appsettings.json,
/// practice-results.db, window-sizes.json) as a single ZIP archive. Imports replace only
/// the entries present in the archive and make the change live in the running app. The
/// original data is automatically backed up before any replacement. The multi-file apply
/// is non-transactional: on a mid-way failure the already replaced files stay replaced,
/// and the automatic backup remains available for recovery.
/// </summary>
public sealed class UserBackupService : IUserBackupService
{
    private const string AppSettingsEntryName = "appsettings.json";
    private const string DatabaseEntryName = "practice-results.db";
    private const string WindowSizesEntryName = "window-sizes.json";

    // The first 16 bytes of every SQLite database file (ASCII "SQLite format 3" + NUL).
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    // The same options ConfigurationStore persists with, so the exported settings entry is
    // byte-equivalent to the per-user appsettings.json on disk.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppPaths _appPaths;
    private readonly IConfigurationService _configurationService;
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private readonly IWindowSizeStore _windowSizeStore;
    private readonly ILogger<UserBackupService> _logger;

    public UserBackupService(
        IAppPaths appPaths,
        IConfigurationService configurationService,
        IPracticeResultStatisticsStore statisticsStore,
        IWindowSizeStore windowSizeStore,
        ILogger<UserBackupService> logger)
    {
        _appPaths = appPaths;
        _configurationService = configurationService;
        _statisticsStore = statisticsStore;
        _windowSizeStore = windowSizeStore;
        _logger = logger;
    }

    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        try
        {
            // Serialize the live configuration on the caller's (UI) thread before the
            // first await: the configuration owner documents Current as UI-thread-only.
            // The live state is exported rather than the on-disk file because that file
            // may not exist yet (fresh profile) or may have no per-user location at all,
            // while the live state is always the complete, authoritative one.
            var settingsJson = JsonSerializer.Serialize(_configurationService.Current, JsonOptions);

            byte[]? windowSizesBytes = null;
            // The file name matches WindowSizeStore's own backing file.
            var windowSizesPath = Path.Combine(_appPaths.AppDataDirectory, WindowSizesEntryName);
            if (File.Exists(windowSizesPath))
            {
                windowSizesBytes = await File.ReadAllBytesAsync(windowSizesPath, cancellationToken);
            }

            // VACUUM INTO refuses to overwrite, so the temporary copy gets a fresh name.
            var tempDatabasePath = Path.Combine(Path.GetTempPath(), $"pentagrammata-{Guid.NewGuid():N}.db");
            try
            {
                await _statisticsStore.CreateConsistentCopyAsync(tempDatabasePath, cancellationToken);

                using var archive = new ZipArchive(destination, ZipArchiveMode.Create);
                WriteEntry(archive, AppSettingsEntryName, System.Text.Encoding.UTF8.GetBytes(settingsJson));
                WriteEntry(archive, DatabaseEntryName, await File.ReadAllBytesAsync(tempDatabasePath, cancellationToken));
                if (windowSizesBytes is not null)
                {
                    WriteEntry(archive, WindowSizesEntryName, windowSizesBytes);
                }
            }
            finally
            {
                if (File.Exists(tempDatabasePath))
                {
                    File.Delete(tempDatabasePath);
                }
            }
        }
        catch (UserBackupException)
        {
            throw;
        }
        catch (StatisticsStoreException ex)
        {
            throw new UserBackupException("Could not export the practice statistics database.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to export user data to a backup archive");
            throw new UserBackupException("Could not export the backup archive.", ex);
        }
    }

    public BackupContents? InspectArchive(Stream archive)
    {
        var seekable = EnsureSeekable(archive);
        try
        {
            using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            var contents = ReadContents(zip);
            return contents.Any ? contents : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (!ReferenceEquals(seekable, archive))
            {
                seekable.Dispose();
            }
        }
    }

    public async Task<BackupImportResult> ImportAsync(Stream archive, CancellationToken cancellationToken = default)
    {
        // Drain any in-flight configuration save before the settings file can be
        // overwritten; no new save can be requested in between, because every
        // configuration mutation is initiated on the UI thread, which import holds.
        await _configurationService.FlushAsync();

        var seekable = EnsureSeekable(archive);
        string? backupPath = null;
        try
        {
            using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            var contents = ReadContents(zip);
            if (!contents.Any)
            {
                throw new UserBackupException("The selected file is not a PentaGrammata backup archive.");
            }

            string? tempDatabasePath = null;
            try
            {
                // Validate everything before replacing anything, so a malformed archive
                // cannot destroy the live data part-way through the import.
                AppConfig? importedSettings = null;
                byte[]? settingsBytes = null;
                byte[]? windowSizesBytes = null;

                if (contents.Database)
                {
                    tempDatabasePath = Path.Combine(Path.GetTempPath(), $"pentagrammata-{Guid.NewGuid():N}.db");
                    await using (var databaseStream = File.Create(tempDatabasePath))
                    {
                        var entry = FindEntry(zip, DatabaseEntryName)!;
                        await entry.Open().CopyToAsync(databaseStream, cancellationToken);
                    }

                    ValidateSqliteDatabase(tempDatabasePath);
                }

                if (contents.AppSettings)
                {
                    settingsBytes = ReadEntryBytes(zip, AppSettingsEntryName);
                    importedSettings = ParseSettings(settingsBytes);
                }

                if (contents.WindowSizes)
                {
                    windowSizesBytes = ReadEntryBytes(zip, WindowSizesEntryName);
                    ValidateWindowSizes(windowSizesBytes);
                }

                backupPath = await CreateAutomaticBackupAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Apply database → window sizes → settings: a failure mid-way leaves the
                // configuration, the hardest to rebuild, untouched.
                if (contents.Database)
                {
                    await _statisticsStore.ReplaceDatabaseAsync(tempDatabasePath!, cancellationToken);
                }

                if (windowSizesBytes is not null)
                {
                    var directory = _appPaths.AppDataDirectory;
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, WindowSizesEntryName), windowSizesBytes);
                    _windowSizeStore.InvalidateCache();
                }

                if (settingsBytes is not null)
                {
                    var configPath = _appPaths.PreferredUserConfigPath;
                    if (configPath is not null)
                    {
                        var directory = Path.GetDirectoryName(configPath);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.WriteAllBytes(configPath, settingsBytes);
                        _configurationService.ReloadFromDisk();
                    }
                    else
                    {
                        // No per-user config file on this platform: apply in memory for
                        // this session, matching how persistence behaves here.
                        _configurationService.ApplyImported(importedSettings!);
                    }
                }

                return new BackupImportResult(contents, backupPath);
            }
            finally
            {
                if (tempDatabasePath is not null && File.Exists(tempDatabasePath))
                {
                    File.Delete(tempDatabasePath);
                }
            }
        }
        catch (UserBackupException ex) when (backupPath is not null)
        {
            throw new UserBackupException(ex.Message, ex, backupPath);
        }
        catch (UserBackupException)
        {
            throw;
        }
        catch (StatisticsStoreException ex)
        {
            throw new UserBackupException("Could not replace the practice statistics database.", ex, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException)
        {
            _logger.LogError(ex, "Failed to import user data from a backup archive");
            throw new UserBackupException("Could not import the backup archive.", ex, backupPath);
        }
        finally
        {
            if (!ReferenceEquals(seekable, archive))
            {
                seekable.Dispose();
            }
        }
    }

    private async Task<string> CreateAutomaticBackupAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(Path.Combine(_appPaths.AppDataDirectory, "backups"));
        var backupPath = Path.Combine(directory, $"pentagrammata-before-import-{DateTime.Now:yyyy-MM-dd-HHmmss}-{Guid.NewGuid():N}.zip");
        var temporaryPath = backupPath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await ExportAsync(stream, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, backupPath);
            return backupPath;
        }
        catch (Exception ex) when (ex is UserBackupException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to create automatic backup before import at {BackupPath}", backupPath);
            throw new UserBackupException("Could not create the automatic backup. No data was replaced.", ex);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not remove incomplete automatic backup at {TemporaryPath}", temporaryPath);
            }
        }
    }

    private static BackupContents ReadContents(ZipArchive archive)
    {
        // Only the three exact flat names are recognized, which also rules out path
        // traversal inside the archive.
        return new BackupContents(
            FindEntry(archive, AppSettingsEntryName) is not null,
            FindEntry(archive, DatabaseEntryName) is not null,
            FindEntry(archive, WindowSizesEntryName) is not null);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryName)
    {
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] ReadEntryBytes(ZipArchive archive, string entryName)
    {
        using var entryStream = FindEntry(archive, entryName)!.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void ValidateSqliteDatabase(string path)
    {
        var header = new byte[SqliteHeader.Length];
        using var stream = File.OpenRead(path);
        var totalRead = 0;
        while (totalRead < header.Length)
        {
            var read = stream.Read(header, totalRead, header.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead < header.Length || !header.AsSpan().SequenceEqual(SqliteHeader))
        {
            throw new UserBackupException("The backup contains an invalid practice-results database.");
        }
    }

    private static void ValidateWindowSizes(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new UserBackupException("The backup contains an invalid window-sizes file.");
        }
    }

    private static AppConfig ParseSettings(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(bytes, JsonOptions) ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            throw new UserBackupException("The backup contains an invalid settings file.", ex);
        }
    }

    /// <summary>
    /// ZipArchive in read mode needs a seekable stream; picker-provided streams always are,
    /// but a non-seekable one is buffered in memory rather than rejected.
    /// </summary>
    private static Stream EnsureSeekable(Stream stream)
    {
        if (stream.CanSeek)
        {
            return stream;
        }

        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        return buffer;
    }
}
