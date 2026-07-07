using System.Threading.Tasks;

using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class UiSettingsDialogService : IUiSettingsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;

    public UiSettingsDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
    }

    public async Task<UiPreferences?> ShowUiSettingsDialogAsync(UiPreferences current)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var viewModel = _viewModelFactory.CreateUiSettings(current);
        var dialog = new UiSettingsDialog
        {
            DataContext = viewModel
        };

        var saved = await dialog.ShowDialog<bool>(owner);
        return saved ? viewModel.BuildPreferences() : null;
    }
}
