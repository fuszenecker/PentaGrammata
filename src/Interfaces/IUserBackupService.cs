using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Which of the known backup entries an archive contains.
/// </summary>
public sealed record BackupContents(bool AppSettings, bool Database, bool WindowSizes)
{
    public bool Any => AppSettings || Database || WindowSizes;
}

/// <summary>
/// Exports and imports the user's data files (per-user appsettings.json,
/// practice-results.db, window-sizes.json) as a single ZIP archive, and makes an import
/// live in the running application (configuration reload, statistics-store reset,
/// window-size cache invalidation).
/// </summary>
public interface IUserBackupService
{
    /// <summary>
    /// Writes a backup archive to <paramref name="destination"/>. The settings entry is
    /// serialized from the live configuration (the file may not exist on disk on a fresh
    /// profile or on platforms without a per-user config file); the database entry is a
    /// consistent snapshot of the live database; the window-sizes entry is omitted when no
    /// such file exists.
    /// </summary>
    Task ExportAsync(Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the known entries present in the archive, or <c>null</c> when the stream is
    /// not a readable ZIP archive containing at least one recognized entry.
    /// </summary>
    BackupContents? InspectArchive(Stream archive);

    /// <summary>
    /// Replaces the on-disk user data with the archive's recognized entries and makes the
    /// change live. Only the entries present in the archive are replaced; entries
    /// recognized are the three flat names appsettings.json, practice-results.db and
    /// window-sizes.json (anything else in the archive is ignored, which also rules out
    /// path traversal). Throws <see cref="PentaGrammata.Exceptions.UserBackupException"/> with a user-facing
    /// message on any failure. Before replacement, creates a uniquely named backup in
    /// the app data directory's backups subdirectory; if backup creation fails, no entries
    /// are replaced. The service keeps a temporary rollback snapshot while applying the
    /// import and restores disk and live state if any apply step fails. Must be called on the UI thread: it mutates the live
    /// configuration, which its owner documents as UI-thread-only.
    /// </summary>
    Task<BackupImportResult> ImportAsync(Stream archive, CancellationToken cancellationToken = default);

    /// <summary>Restores the most recent pre-import archive, while retaining a pre-restore archive.</summary>
    Task<BackupImportResult> RestoreLatestAsync(CancellationToken cancellationToken = default);
}
