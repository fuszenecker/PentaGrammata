using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

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

        _windowSizeService.Track(dialog);
        await dialog.ShowDialog(owner);
    }
}
