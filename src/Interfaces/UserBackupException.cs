using System;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Thrown by <see cref="IUserBackupService"/> when an export or import fails, or when a
/// selected archive is not a valid PentaGrammata backup. The message is ready to show to
/// the user, so the presenting layer can surface it directly.
/// </summary>
public sealed class UserBackupException : Exception
{
    public UserBackupException(string message)
        : base(message)
    {
    }

    public UserBackupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
