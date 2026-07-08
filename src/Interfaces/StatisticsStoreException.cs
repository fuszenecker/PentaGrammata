using System;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Thrown by <see cref="IPracticeResultStatisticsStore"/> when a statistics
/// operation fails. This shields callers (e.g. view models) from having to know
/// the underlying storage technology or catch storage-specific exception types.
/// </summary>
public sealed class StatisticsStoreException : Exception
{
    public StatisticsStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
