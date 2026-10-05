using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Presentation;

/// <summary>Owns the trends CSV save dialog and the asynchronous write to the chosen file.</summary>
public interface ITrendsCsvExportService
{
    /// <returns><c>true</c> when written; <c>false</c> when no owner or the picker was cancelled.</returns>
    Task<bool> ExportAsync(string csv, CancellationToken cancellationToken = default);
}
