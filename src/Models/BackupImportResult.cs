using PentaGrammata.Interfaces;

namespace PentaGrammata.Models;

/// <summary>
/// The imported entries and the archive preserving the user's data before replacement.
/// </summary>
public sealed record BackupImportResult(BackupContents Contents, string BackupPath);
