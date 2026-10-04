using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Presentation;

public sealed class DialogViewModelFactory : IDialogViewModelFactory
{
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IAudioHeadroomAnalyzer _headroomAnalyzer;
    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IPracticeStatisticsExporter _statisticsExporter;
    private readonly IConfusionAnalysisService _confusionAnalysisService;
    private readonly IConfusionObservationExtractor _confusionExtractor;
    private readonly ICorrelationAnalysisService _correlationAnalysisService;
    private readonly IInfoDialogService _infoDialogService;
    private readonly IConfigurationService _configurationService;

    public DialogViewModelFactory(
        IPracticeSettingsValidator settingsValidator,
        IAudioHeadroomAnalyzer headroomAnalyzer,
        IPracticeResultStatisticsService statisticsService,
        IPracticeStatisticsExporter statisticsExporter,
        IConfusionAnalysisService confusionAnalysisService,
        IConfusionObservationExtractor confusionExtractor,
        ICorrelationAnalysisService correlationAnalysisService,
        IInfoDialogService infoDialogService,
        IConfigurationService configurationService)
    {
        _settingsValidator = settingsValidator;
        _headroomAnalyzer = headroomAnalyzer;
        _statisticsService = statisticsService;
        _statisticsExporter = statisticsExporter;
        _confusionAnalysisService = confusionAnalysisService;
        _confusionExtractor = confusionExtractor;
        _correlationAnalysisService = correlationAnalysisService;
        _infoDialogService = infoDialogService;
        _configurationService = configurationService;
    }

    public MorseSettingsDialogViewModel CreateMorseSettings(AppConfig currentSettings)
    {
        return new MorseSettingsDialogViewModel(currentSettings, _settingsValidator, _headroomAnalyzer);
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
            _confusionExtractor,
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

    public CorrelationDialogViewModel CreateCorrelation()
    {
        return new CorrelationDialogViewModel(_statisticsService, _configurationService, _correlationAnalysisService);
    }

    public ConfirmDialogViewModel CreateConfirm(string title, string message, string confirmButtonText)
    {
        return new ConfirmDialogViewModel(title, message, confirmButtonText);
    }

    public InfoDialogViewModel CreateInfo(string title, string primaryMessage, string? detailMessage, bool showDoNotShowAgain, string? detailHeading)
    {
        return new InfoDialogViewModel(title, primaryMessage, detailMessage, showDoNotShowAgain, detailHeading);
    }
}
