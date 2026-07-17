using AppConfig = PentaGrammata.Configuration.AppConfiguration;
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
    private readonly IConfigurationService _configurationService;

    public DialogViewModelFactory(
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService,
        IConfigurationService configurationService)
    {
        _settingsValidator = settingsValidator;
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;
        _configurationService = configurationService;
    }

    public MorseSettingsDialogViewModel CreateMorseSettings(AppConfig currentSettings)
    {
        return new MorseSettingsDialogViewModel(currentSettings, _settingsValidator);
    }

    public PracticeResultWindowViewModel CreatePracticeResult(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise)
    {
        return new PracticeResultWindowViewModel(
            result,
            characterWpm,
            averageWpm,
            alreadySaved,
            errorThresholdPercent,
            noise,
            _statisticsStore,
            _infoDialogService);
    }

    public UiSettingsDialogViewModel CreateUiSettings(UiPreferences current)
    {
        return new UiSettingsDialogViewModel(current);
    }

    public AboutWindowViewModel CreateAbout()
    {
        return new AboutWindowViewModel();
    }

    public TrendsDialogViewModel CreateTrends()
    {
        return new TrendsDialogViewModel(_statisticsStore);
    }

    public ConfusionsDialogViewModel CreateConfusions()
    {
        return new ConfusionsDialogViewModel(_statisticsStore, _configurationService);
    }
}
