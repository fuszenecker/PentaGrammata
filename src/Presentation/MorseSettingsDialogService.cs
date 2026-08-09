using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Presentation;

public sealed class MorseSettingsDialogService : IMorseSettingsDialogService
{
    private readonly IWindowContext _windowContext;
    private readonly IDialogViewModelFactory _viewModelFactory;
    private readonly IWindowSizeService _windowSizeService;

    public MorseSettingsDialogService(IWindowContext windowContext, IDialogViewModelFactory viewModelFactory, IWindowSizeService windowSizeService)
    {
        _windowContext = windowContext;
        _viewModelFactory = viewModelFactory;
        _windowSizeService = windowSizeService;
    }

    public async Task<AppConfig?> ShowSettingsDialogAsync(AppConfig currentSettings)
    {
        var owner = _windowContext.MainWindow;
        if (owner is null)
        {
            return null;
        }

        var viewModel = _viewModelFactory.CreateMorseSettings(currentSettings);
        var dialog = new MorseSettingsDialog
        {
            DataContext = viewModel
        };

        _windowSizeService.Track(dialog);
        var saved = await dialog.ShowDialog<bool>(owner);
        if (!saved)
        {
            return null;
        }

        return viewModel.TryBuildSettings(out var newSettings) ? newSettings : null;
    }
}
