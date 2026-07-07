using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Services;

public sealed class DialogViewModelFactory : IDialogViewModelFactory
{
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private readonly IInfoDialogService _infoDialogService;

    public DialogViewModelFactory(
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService)
    {
        _settingsValidator = settingsValidator;
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;
    }

    public MorseSettingsDialogViewModel CreateMorseSettings(AppConfig currentSettings)
    {
        return new MorseSettingsDialogViewModel(currentSettings, _settingsValidator);
    }

    public PracticeResultWindowViewModel CreatePracticeResult(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        bool alreadySaved)
    {
        return new PracticeResultWindowViewModel(
            result, characterWpm, averageWpm, alreadySaved, _statisticsStore, _infoDialogService);
    }

    public UiSettingsDialogViewModel CreateUiSettings(UiPreferences current)
    {
        return new UiSettingsDialogViewModel(current);
    }

    public AboutWindowViewModel CreateAbout()
    {
        return new AboutWindowViewModel();
    }
}
