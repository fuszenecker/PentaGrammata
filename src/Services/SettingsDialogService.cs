using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IPracticeSettingsValidator _settingsValidator;

    public SettingsDialogService(IWindowContext windowContext, IPracticeSettingsValidator settingsValidator)
    {
        _windowContext = windowContext;
        _settingsValidator = settingsValidator;
    }

    public async Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var viewModel = new SettingsDialogViewModel(currentSettings, _settingsValidator);
        var dialog = new SettingsDialog
        {
            DataContext = viewModel
        };

        var saved = await dialog.ShowDialog<bool>(owner);
        if (!saved)
        {
            return null;
        }

        return viewModel.TryBuildSettings(out var newSettings) ? newSettings : null;
    }
}
