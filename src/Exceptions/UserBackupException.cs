using System;

namespace PentaGrammata.Exceptions;

/// <summary>
/// Thrown by <see cref="IUserBackupService"/> when an export or import fails, or when a
/// selected archive is not a valid PentaGrammata backup. The message is ready to show to
/// the user, so the presenting layer can surface it directly.
/// </summary>
public sealed class UserBackupException : Exception
{
    /// <summary>
    /// Full path of the completed automatic backup, when an import fails after creating it.
    /// </summary>
    public string? BackupPath { get; }

    public UserBackupException(string message)
        : base(message)
    {
    }

    public UserBackupException(string message, Exception innerException, string? backupPath = null)
        : base(message, innerException)
    {
        BackupPath = backupPath;
    }
}
