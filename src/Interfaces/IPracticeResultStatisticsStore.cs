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
}
