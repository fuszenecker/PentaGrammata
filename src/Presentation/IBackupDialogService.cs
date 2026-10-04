using System.Threading.Tasks;

namespace PentaGrammata.Presentation;

/// <summary>
/// Outcome of a File → Export/Import interaction: <see cref="Completed"/> means the
/// operation finished successfully; anything the user backed out of (or that failed) is
/// <see cref="Cancelled"/>.
/// </summary>
public enum BackupDialogOutcome
{
    Cancelled,
    Completed,
}

public interface IBackupDialogService
{
    /// <summary>
    /// Runs the export flow: save-file picker for the backup ZIP, then the archive is
    /// written. Failures are reported in an info dialog.
    /// </summary>
    Task<BackupDialogOutcome> ShowExportAsync();

    /// <summary>
    /// Runs the import flow: open-file picker for the backup ZIP, a confirmation dialog
    /// asking whether to replace the current data, then the files are replaced and the
    /// change is made live. Failures are reported in an info dialog.
    /// </summary>
    Task<BackupDialogOutcome> ShowImportAsync();
}
