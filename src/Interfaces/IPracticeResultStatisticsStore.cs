using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeResultStatisticsStore
{
    Task SaveAsync(PracticeResultStatisticsRecord record, CancellationToken cancellationToken = default);
}
