using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Services;

public sealed class SettingsDialogService : ISettingsDialogService
{
    public async Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings)
    {
        var owner = GetOwnerWindow();
        if (owner is null)
        {
            return null;
        }

        var viewModel = new SettingsDialogViewModel(currentSettings);
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

    private static Window? GetOwnerWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
