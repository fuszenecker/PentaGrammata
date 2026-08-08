using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class ConfusionsDialogService : IConfusionsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public ConfusionsDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory, IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
    }

    public async Task ShowConfusionsAsync()
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return;
        }

        var viewModel = _viewModelFactory.CreateConfusions();
        await viewModel.InitializeAsync().ConfigureAwait(true);

        var dialog = new ConfusionsDialog
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(dialog);
        await dialog.ShowDialog(owner);
    }
}
