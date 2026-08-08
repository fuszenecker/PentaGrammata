using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Services;

public sealed class DialogViewModelFactory : IDialogViewModelFactory
{
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IPracticeStatisticsExporter _statisticsExporter;
    private readonly IConfusionAnalysisService _confusionAnalysisService;
    private readonly IInfoDialogService _infoDialogService;
    private readonly IConfigurationService _configurationService;

    public DialogViewModelFactory(
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultStatisticsService statisticsService,
        IPracticeStatisticsExporter statisticsExporter,
        IConfusionAnalysisService confusionAnalysisService,
        IInfoDialogService infoDialogService,
        IConfigurationService configurationService)
    {
        _settingsValidator = settingsValidator;
        _statisticsService = statisticsService;
        _statisticsExporter = statisticsExporter;
        _confusionAnalysisService = confusionAnalysisService;
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
            _statisticsService,
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
        return new TrendsDialogViewModel(_statisticsService, _statisticsExporter);
    }

    public ConfusionsDialogViewModel CreateConfusions()
    {
        return new ConfusionsDialogViewModel(_statisticsService, _configurationService, _confusionAnalysisService);
    }
}
