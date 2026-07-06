using System.Threading.Tasks;

using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class UiSettingsDialogService : IUiSettingsDialogService
{
    private readonly IWindowContext _windowContext;

    public UiSettingsDialogService(IWindowContext windowContext)
    {
        _windowContext = windowContext;
    }

    public async Task<UiPreferences?> ShowUiSettingsDialogAsync(UiPreferences current)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var viewModel = new UiSettingsDialogViewModel(current);
        var dialog = new UiSettingsDialog
        {
            DataContext = viewModel
        };

        var saved = await dialog.ShowDialog<bool>(owner);
        return saved ? viewModel.BuildPreferences() : null;
    }
}
