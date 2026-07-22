using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class ConfusionsDialogService : IConfusionsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;

    public ConfusionsDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
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

        await dialog.ShowDialog(owner);
    }
}
