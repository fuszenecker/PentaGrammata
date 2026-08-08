using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Pass-through implementation of <see cref="IPracticeResultStatisticsService"/> that
/// delegates to <see cref="IPracticeResultStatisticsStore"/>. Kept deliberately thin:
/// it exists to give view models a service facade rather than direct store access.
/// </summary>
public sealed class PracticeResultStatisticsService : IPracticeResultStatisticsService
{
    private readonly IPracticeResultStatisticsStore _store;

    public PracticeResultStatisticsService(IPracticeResultStatisticsStore store)
    {
        _store = store;
    }

    public string DatabasePath => _store.DatabasePath;

    public Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default)
        => _store.SaveAsync(record, cancellationToken);

    public Task<IReadOnlyList<PracticeResultStatisticsRecord>> GetStatisticsRecordsAsync(CancellationToken cancellationToken = default)
        => _store.GetStatisticsRecordsAsync(cancellationToken);

    public Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default)
        => _store.GetConfusionObservationsAsync(cancellationToken);
}
