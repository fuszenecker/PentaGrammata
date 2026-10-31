using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Exceptions;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using Promptuarium.Serializer;

namespace PentaGrammata.Services;

/// <summary>
/// Archives and restores the user's data (per-user settings.prmt and practice-results.db)
/// as a single PPAK1-LE Promptuarium archive (<see cref="BackupArchive"/>). Window sizes are
/// not part of a backup. Imports replace only
/// the entries present in the archive and make the change live in the running app. The
/// original data is automatically backed up before replacement. An in-process rollback
/// snapshot also restores the complete prior profile when any apply step fails.
/// </summary>
public sealed class UserBackupService : IUserBackupService
{
    private const string AppSettingsEntryName = ConfigurationPaths.SettingsFileName;
    private const string DatabaseEntryName = "practice-results.db";
    private static readonly string[] EntryNames = [AppSettingsEntryName, DatabaseEntryName];

    // The first 16 bytes of every SQLite database file (ASCII "SQLite format 3" + NUL).
    private static readonly byte[] SqliteHeader = "SQLite format 3\0"u8.ToArray();

    private readonly IAppPaths _appPaths;
    private readonly IConfigurationService _configurationService;
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private readonly ILogger<UserBackupService> _logger;

    public UserBackupService(
        IAppPaths appPaths,
        IConfigurationService configurationService,
        IPracticeResultStatisticsStore statisticsStore,
        ILogger<UserBackupService> logger)
    {
        _appPaths = appPaths;
        _configurationService = configurationService;
        _statisticsStore = statisticsStore;
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
            // Serialized as ConfigurationStore persists it, so the exported settings entry is
            // byte-equivalent to the per-user settings.prmt on disk.
            var settingsBytes = PromptuariumSerializer.Serialize(_configurationService.Current, ConfigurationFormat.Options);

            // VACUUM INTO refuses to overwrite, so the temporary copy gets a fresh name.
            var tempDatabasePath = Path.Combine(Path.GetTempPath(), $"pentagrammata-{Guid.NewGuid():N}.db");
            try
            {
                await _statisticsStore.CreateConsistentCopyAsync(tempDatabasePath, cancellationToken);

                var databaseBytes = await File.ReadAllBytesAsync(tempDatabasePath, cancellationToken);
                BackupArchive.Write(destination, [(AppSettingsEntryName, settingsBytes), (DatabaseEntryName, databaseBytes)]);
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
        try
        {
            var contents = ReadContents(BackupArchive.Read(archive, EntryNames));
            return contents.Any ? contents : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    public async Task<BackupImportResult> ImportAsync(Stream archive, CancellationToken cancellationToken = default)
    {
        return await ImportCoreAsync(archive, cancellationToken, createAutomaticBackup: true);
    }

    public async Task<BackupImportResult> RestoreLatestAsync(CancellationToken cancellationToken = default)
    {
        string? safetyBackupPath = null;
        try
        {
            var backupDirectory = Path.Combine(_appPaths.AppDataDirectory, "backups");
            var restorePoint = Directory.Exists(backupDirectory)
                ? Directory.GetFiles(backupDirectory, $"pentagrammata-before-import-*{IUserBackupService.FileExtension}")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
            if (restorePoint is null)
            {
                throw new UserBackupException("No pre-import restore point is available.");
            }

            safetyBackupPath = await CreateAutomaticBackupAsync(cancellationToken, "before-restore");
            await using var stream = new FileStream(restorePoint, FileMode.Open, FileAccess.Read, FileShare.Read);
            var result = await ImportCoreAsync(stream, cancellationToken, createAutomaticBackup: false);
            return new BackupImportResult(result.Contents, safetyBackupPath);
        }
        catch (UserBackupException ex)
        {
            throw new UserBackupException(ex.Message, ex, safetyBackupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogError(ex, "Failed to restore the latest pre-import archive");
            throw new UserBackupException("Could not restore the latest pre-import archive.", ex, safetyBackupPath);
        }
    }

    private async Task<BackupImportResult> ImportCoreAsync(
        Stream archive,
        CancellationToken cancellationToken,
        bool createAutomaticBackup)
    {
        string? recoveryArchivePath = null;
        // Drain any in-flight save first. The main-window controls stay disabled during
        // the commit/rollback interval so user-driven settings mutations cannot enqueue
        // another write while imported files are being published.
        await _configurationService.FlushAsync();

        try
        {
            Dictionary<string, byte[]> files;
            try
            {
                files = BackupArchive.Read(archive, EntryNames);
            }
            catch (InvalidDataException ex)
            {
                throw new UserBackupException("The selected file is not a valid PentaGrammata backup archive.", ex);
            }

            var contents = ReadContents(files);
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

                if (contents.Database)
                {
                    tempDatabasePath = Path.Combine(Path.GetTempPath(), $"pentagrammata-{Guid.NewGuid():N}.db");
                    await File.WriteAllBytesAsync(tempDatabasePath, files[DatabaseEntryName], cancellationToken);

                    ValidateSqliteDatabase(tempDatabasePath);
                }

                if (contents.AppSettings)
                {
                    settingsBytes = files[AppSettingsEntryName];
                    importedSettings = ParseSettings(settingsBytes);
                }

                var databaseExistedBeforeImport = contents.Database && File.Exists(_statisticsStore.DatabasePath);
                recoveryArchivePath = createAutomaticBackup
                    ? await CreateAutomaticBackupAsync(cancellationToken, "before-import")
                    : null;
                cancellationToken.ThrowIfCancellationRequested();

                var previousConfiguration = _configurationService.Current.Clone();
                var configurationPath = _appPaths.PreferredUserConfigPath;
                var previousConfigurationFile = CaptureFile(configurationPath);
                string? rollbackDatabasePath = null;
                var databaseExisted = databaseExistedBeforeImport;
                if (databaseExisted)
                {
                    rollbackDatabasePath = Path.Combine(Path.GetTempPath(), $"pentagrammata-rollback-{Guid.NewGuid():N}.db");
                    await _statisticsStore.CreateConsistentCopyAsync(rollbackDatabasePath, cancellationToken);
                }

                try
                {
                    // Every replacement happens only after a complete rollback snapshot
                    // exists. The settings write uses a sibling temp file and an atomic
                    // move, so a failed write preserves its target.
                    if (contents.Database)
                    {
                        await _statisticsStore.ReplaceDatabaseAsync(tempDatabasePath!, cancellationToken);
                    }

                    if (settingsBytes is not null)
                    {
                        if (configurationPath is not null)
                        {
                            await ReplaceFileAtomicallyAsync(configurationPath, settingsBytes, cancellationToken);
                            _configurationService.ReloadFromDisk();
                        }
                        else
                        {
                            // No per-user config file on this platform: apply in memory for
                            // this session, matching how persistence behaves here.
                            _configurationService.ApplyImported(importedSettings!);
                        }
                    }

                    return new BackupImportResult(contents, recoveryArchivePath ?? string.Empty);
                }
                catch (Exception applyException)
                {
                    var rollbackErrors = await RollbackAsync(
                        contents,
                        rollbackDatabasePath,
                        databaseExisted,
                        configurationPath,
                        previousConfigurationFile,
                        previousConfiguration);
                    if (rollbackErrors.Count > 0)
                    {
                        rollbackErrors.Insert(0, applyException);
                        throw new UserBackupException(
                            "Import failed and rollback was incomplete. See the application log for recovery details.",
                            new AggregateException(rollbackErrors));
                    }

                    throw new UserBackupException("Import failed. The previous data was restored.", applyException);
                }
                finally
                {
                    if (rollbackDatabasePath is not null && File.Exists(rollbackDatabasePath))
                    {
                        File.Delete(rollbackDatabasePath);
                    }
                }
            }
            finally
            {
                if (tempDatabasePath is not null && File.Exists(tempDatabasePath))
                {
                    File.Delete(tempDatabasePath);
                }
            }
        }
        catch (UserBackupException ex) when (recoveryArchivePath is not null && ex.BackupPath is null)
        {
            throw new UserBackupException(ex.Message, ex, recoveryArchivePath);
        }
        catch (UserBackupException)
        {
            throw;
        }
        catch (StatisticsStoreException ex)
        {
            throw new UserBackupException("Could not replace the practice statistics database.", ex, recoveryArchivePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            _logger.LogError(ex, "Failed to import user data from a backup archive");
            throw new UserBackupException("Could not import the backup archive.", ex, recoveryArchivePath);
        }
    }

    private async Task<string> CreateAutomaticBackupAsync(CancellationToken cancellationToken, string kind)
    {
        var directory = Path.GetFullPath(Path.Combine(_appPaths.AppDataDirectory, "backups"));
        var backupPath = Path.Combine(directory, $"pentagrammata-{kind}-{DateTime.Now:yyyy-MM-dd-HHmmss}-{Guid.NewGuid():N}{IUserBackupService.FileExtension}");
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
            _logger.LogError(ex, "Failed to create automatic recovery archive at {BackupPath}", backupPath);
            throw new UserBackupException("Could not create the recovery archive. No data was replaced.", ex);
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

    private async Task<List<Exception>> RollbackAsync(
        BackupContents contents,
        string? rollbackDatabasePath,
        bool databaseExisted,
        string? configurationPath,
        FileSnapshot configurationFile,
        AppConfig previousConfiguration)
    {
        var errors = new System.Collections.Generic.List<Exception>();
        if (contents.Database)
        {
            try
            {
                if (databaseExisted && rollbackDatabasePath is not null)
                {
                    await _statisticsStore.ReplaceDatabaseAsync(rollbackDatabasePath, CancellationToken.None);
                }
                else
                {
                    await _statisticsStore.DeleteDatabaseAsync(CancellationToken.None);
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        }

        if (contents.AppSettings)
        {
            if (configurationPath is not null)
            {
                try
                {
                    await RestoreFileAsync(configurationPath, configurationFile);
                }
                catch (Exception ex) { errors.Add(ex); }
            }

            try
            {
                _configurationService.ApplyImported(previousConfiguration);
            }
            catch (Exception ex) { errors.Add(ex); }
        }

        if (errors.Count > 0)
        {
            _logger.LogError(new AggregateException(errors), "Import rollback did not fully restore the previous profile");
        }

        return errors;
    }

    private static FileSnapshot CaptureFile(string? path)
    {
        return path is not null && File.Exists(path)
            ? new FileSnapshot(true, File.ReadAllBytes(path))
            : new FileSnapshot(false, null);
    }

    private static async Task ReplaceFileAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task RestoreFileAsync(string path, FileSnapshot snapshot)
    {
        if (snapshot.Existed)
        {
            await ReplaceFileAtomicallyAsync(path, snapshot.Bytes!, CancellationToken.None);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record FileSnapshot(bool Existed, byte[]? Bytes);

    private static BackupContents ReadContents(IReadOnlyDictionary<string, byte[]> files)
    {
        return new BackupContents(files.ContainsKey(AppSettingsEntryName), files.ContainsKey(DatabaseEntryName));
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

    private static AppConfig ParseSettings(byte[] bytes)
    {
        try
        {
            return PromptuariumSerializer.Deserialize<AppConfig>(bytes, ConfigurationFormat.Options);
        }
        catch (InvalidDataException ex)
        {
            throw new UserBackupException("The backup contains an invalid settings file.", ex);
        }
    }
}
