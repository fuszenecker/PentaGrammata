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

    Task<IReadOnlyList<ConfusionObservation>> GetConfusionObservationsAsync(CancellationToken cancellationToken = default);
}
