using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class CorrelationDialogService : ICorrelationDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public CorrelationDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory, IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
    }

    public async Task ShowCorrelationAsync()
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return;
        }

        var viewModel = _viewModelFactory.CreateCorrelation();
        await viewModel.InitializeAsync().ConfigureAwait(true);

        var dialog = new CorrelationDialog
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(dialog);
        await dialog.ShowDialog(owner);
    }
}
