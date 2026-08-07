using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultStatisticsStore
{
    string DatabasePath { get; }

    Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PracticeTrendPoint>> GetTrendPointsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every saved session with all persisted columns (the full
    /// <see cref="PracticeResultStatisticsRecord"/>, without the child
    /// confusion rows). <see cref="GetTrendPointsAsync"/> is the smaller
    /// subset used by the trends chart; this is the complete record set.
    /// </summary>
    Task<IReadOnlyList<PracticeResultStatisticsRecord>> GetStatisticsRecordsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default);
}
