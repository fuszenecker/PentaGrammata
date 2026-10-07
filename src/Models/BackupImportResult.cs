using PentaGrammata.Interfaces;

namespace PentaGrammata.Models;

/// <summary>
/// The entries imported and the temporary recovery archive created before the operation.
/// </summary>
public sealed record BackupImportResult(BackupContents Contents, string BackupPath);
