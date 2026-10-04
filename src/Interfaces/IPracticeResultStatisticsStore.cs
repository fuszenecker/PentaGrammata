using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultStatisticsStore
{
    string DatabasePath { get; }

    Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every saved session with all persisted columns (the full
    /// <see cref="PracticeResultStatisticsRecord"/>, without the child
    /// confusion rows).
    /// </summary>
    Task<IReadOnlyList<PracticeResultStatisticsRecord>> GetStatisticsRecordsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a standalone, self-contained copy of the database at
    /// <paramref name="destinationPath"/> (which must not already exist) via
    /// <c>VACUUM INTO</c>. The copy is a consistent snapshot that needs no WAL sidecars,
    /// even while other operations run against the live file. Used by backup export.
    /// </summary>
    Task CreateConsistentCopyAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the database file with the SQLite database at <paramref name="sourcePath"/>:
    /// drains in-flight operations via the operation gate, closes pooled connections,
    /// removes the old file and its <c>-wal</c>/<c>-shm</c> sidecars, copies the source in,
    /// and clears the schema-initialized flag so migrations re-run against the imported
    /// file. Used by backup import.
    /// </summary>
    Task ReplaceDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default);
}
