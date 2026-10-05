using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Platform.Storage;

namespace PentaGrammata.Presentation;

public sealed class TrendsCsvExportService(IWindowContext windowContext) : ITrendsCsvExportService
{
    public async Task<bool> ExportAsync(string csv, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csv);
        cancellationToken.ThrowIfCancellationRequested();

        var owner = windowContext.ActiveWindow;
        if (owner is null)
        {
            return false;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export trends as CSV",
            SuggestedFileName = "pentagrammata-trends.csv",
            DefaultExtension = "csv",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("CSV file") { Patterns = ["*.csv"] },
            ],
        }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        if (file is null)
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(csv);
        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(true);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(true);
        return true;
    }
}
