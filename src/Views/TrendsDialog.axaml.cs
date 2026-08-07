using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using PentaGrammata.ViewModels;

namespace PentaGrammata.Views;

public partial class TrendsDialog : Window
{
    private TrendsDialogViewModel? _viewModel;

    public TrendsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.ExportCsvRequested -= OnExportCsvRequested;
        }

        _viewModel = DataContext as TrendsDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.ExportCsvRequested += OnExportCsvRequested;
        }
    }

    private void OnCloseRequested()
    {
        Close();
    }

    private async void OnExportCsvRequested(string csv)
    {
        await ExportCsvAsync(csv);
    }

    private async Task ExportCsvAsync(string csv)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export trends as CSV",
            SuggestedFileName = "pentagrammata-trends.csv",
            DefaultExtension = "csv",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV file") { Patterns = new[] { "*.csv" } },
            },
        }).ConfigureAwait(true);

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(csv.AsMemory()).ConfigureAwait(true);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.ExportCsvRequested -= OnExportCsvRequested;
        }

        base.OnClosed(e);
    }
}
