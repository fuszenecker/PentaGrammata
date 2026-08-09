using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Thin service facade over <see cref="IPracticeResultStatisticsStore"/>. View models
/// consume statistics through this service rather than the store directly, so the
/// persistence boundary stays behind the service and can be extended (caching, logging,
/// aggregation) without touching callers.
/// </summary>
public interface IPracticeResultStatisticsService
{
    string DatabasePath { get; }

    Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PracticeResultStatisticsRecord>> GetStatisticsRecordsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default);
}
