using System;
using System.Threading.Tasks;

using Avalonia.Controls;

using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class TrendsDialogService : ITrendsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public TrendsDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory, IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
    }

    public async Task ShowTrendsAsync()
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return;
        }

        var viewModel = _viewModelFactory.CreateTrends();
        await viewModel.InitializeAsync().ConfigureAwait(true);

        var dialog = new TrendsDialog
        {
            DataContext = viewModel
        };

        viewModel.CloseRequested += dialog.Close;
        EventHandler<WindowClosingEventArgs> closingHandler = (_, args) =>
        {
            if (viewModel.ExportCsvCommand.IsRunning)
            {
                args.Cancel = true;
            }
        };
        dialog.Closing += closingHandler;
        _windowSizeService.Track(dialog);
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            dialog.Closing -= closingHandler;
            viewModel.CloseRequested -= dialog.Close;
        }
    }
}
