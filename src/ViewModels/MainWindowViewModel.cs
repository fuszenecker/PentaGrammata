using System.Linq;
using System.Threading.Tasks;

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;

namespace PentaGrammata.ViewModels;

/// <summary>
/// Shell view model for the main window: owns the menu/dialog orchestration (settings, UI
/// settings, about, trends, confusions, updates) and the character-set selection shared by the
/// combo and the confusions flow. The practice session itself lives on
/// <see cref="Practice"/>, exposed for the main window's bindings.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPracticeController _practiceController;
    private readonly IConfigurationService _configurationService;
    private readonly IMorseSettingsDialogService _settingsDialogService;
    private readonly IUiSettingsDialogService _uiSettingsDialogService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly ITrendsDialogService _trendsDialogService;
    private readonly IConfusionsDialogService _confusionsDialogService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IInfoDialogService _infoDialogService;
    private readonly ILogger<MainWindowViewModel> _logger;

    // Set while the character-set list is being swapped: the bound ComboBox reacts to a new
    // ItemsSource by pushing its own (now stale) SelectedItem back into the view model, which
    // would otherwise overwrite the controller's freshly chosen set.
    private bool suppressSelectedCharacterSetPush;

    public PracticeViewModel Practice { get; }

    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenUiSettingsCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }
    public IAsyncRelayCommand OpenTrendsCommand { get; }
    public IAsyncRelayCommand OpenConfusionsCommand { get; }
    public IAsyncRelayCommand CheckUpdatesCommand { get; }

    [ObservableProperty]
    private string[] characterSets = [];

    [ObservableProperty]
    private string selectedCharacterSet = "Default";

    [ObservableProperty]
    private FontFamily receivedTextFontFamily = FontFamily.Default;

    [ObservableProperty]
    private double receivedTextFontSize = 20.0;

    public MainWindowViewModel(
        IPracticeController practiceController,
        IConfigurationService configurationService,
        IMorseSettingsDialogService settingsDialogService,
        IUiSettingsDialogService uiSettingsDialogService,
        IAboutDialogService aboutDialogService,
        ITrendsDialogService trendsDialogService,
        IConfusionsDialogService confusionsDialogService,
        IUpdateChecker updateChecker,
        IInfoDialogService infoDialogService,
        PracticeViewModel practice,
        ILogger<MainWindowViewModel> logger)
    {
        _practiceController = practiceController;
        _configurationService = configurationService;
        _settingsDialogService = settingsDialogService;
        _uiSettingsDialogService = uiSettingsDialogService;
        _aboutDialogService = aboutDialogService;
        _trendsDialogService = trendsDialogService;
        _confusionsDialogService = confusionsDialogService;
        _updateChecker = updateChecker;
        _infoDialogService = infoDialogService;
        _logger = logger;
        Practice = practice;

        RefreshCharacterSets();
        ReceivedTextFontFamily = new FontFamily(_configurationService.Current.UiPreferences.ReceivedTextFontFamily);
        ReceivedTextFontSize = _configurationService.Current.UiPreferences.ReceivedTextFontSize;

        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsDialogAsync);
        OpenUiSettingsCommand = new AsyncRelayCommand(OpenUiSettingsDialogAsync);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        OpenTrendsCommand = new AsyncRelayCommand(OpenTrendsAsync);
        OpenConfusionsCommand = new AsyncRelayCommand(OpenConfusionsAsync);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
    }

    public async Task OpenSettingsDialogAsync()
    {
        var newSettings = await _settingsDialogService.ShowSettingsDialogAsync(_practiceController.CreateSettingsSnapshot());
        if (newSettings is null)
            return;

        if (!_practiceController.TryApplySettings(newSettings, out var error))
        {
            Practice.DisplayStatusMessage(error, StatusLevel.Error);
            return;
        }

        RefreshCharacterSets();
        Practice.RefreshFromAppliedSettings();
    }

    public async Task OpenUiSettingsDialogAsync()
    {
        var newPrefs = await _uiSettingsDialogService.ShowUiSettingsDialogAsync(
            _configurationService.Current.UiPreferences);
        if (newPrefs is null)
            return;

        await _configurationService.ApplyUiPreferencesAsync(newPrefs);
        ReceivedTextFontFamily = new FontFamily(newPrefs.ReceivedTextFontFamily);
        ReceivedTextFontSize = newPrefs.ReceivedTextFontSize;
    }

    public Task OpenAboutAsync()
    {
        return _aboutDialogService.ShowAboutAsync();
    }

    public async Task CheckUpdatesAsync()
    {
        var result = await _updateChecker.CheckAsync();

        if (!result.Succeeded)
        {
            await _infoDialogService.ShowInfoAsync("Check for updates", result.Error ?? "Could not check for updates.");
            return;
        }

        if (result.UpdateAvailable)
        {
            var message = $"A new version is available: {result.LatestVersion} (you have {result.CurrentVersion}).";
            if (!string.IsNullOrEmpty(result.ReleaseUrl))
            {
                message += $"\n{result.ReleaseUrl}";
            }

            await _infoDialogService.ShowInfoAsync("Update available", message, detailHeading: "Release page");
        }
        else
        {
            await _infoDialogService.ShowInfoAsync(
                "Check for updates",
                $"You are running the latest version ({result.CurrentVersion}).");
        }
    }

    public Task OpenTrendsAsync()
    {
        return _trendsDialogService.ShowTrendsAsync();
    }

    public async Task OpenConfusionsAsync()
    {
        await _confusionsDialogService.ShowConfusionsAsync();
        RefreshCharacterSets();
    }

    /// <summary>
    /// Re-reads the available character sets and the controller's current selection. The
    /// intended selection is captured before the list is replaced, because assigning
    /// <see cref="CharacterSets"/> makes the bound ComboBox write its stale selection back
    /// into <see cref="SelectedCharacterSet"/>; that push is suppressed so it cannot
    /// overwrite a set just chosen elsewhere (e.g. "Practice confusions").
    /// </summary>
    private void RefreshCharacterSets()
    {
        var selected = _practiceController.SelectedCharacterSet;

        suppressSelectedCharacterSetPush = true;
        try
        {
            CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        }
        finally
        {
            suppressSelectedCharacterSetPush = false;
        }

        SelectedCharacterSet = selected;
    }

    partial void OnSelectedCharacterSetChanged(string value)
    {
        if (suppressSelectedCharacterSetPush)
        {
            return;
        }

        _practiceController.SelectedCharacterSet = value;
    }
}
