using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPracticeController _practiceController;
    private readonly IConfigurationService _configurationService;
    private readonly IMorseSettingsDialogService _settingsDialogService;
    private readonly IUiSettingsDialogService _uiSettingsDialogService;
    private readonly IPracticeResultWindowService _practiceResultWindowService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly ITrendsDialogService _trendsDialogService;
    private readonly IConfusionsDialogService _confusionsDialogService;
    private readonly IUpdateChecker _updateChecker;
    private readonly IInfoDialogService _infoDialogService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private bool isPracticeRunning;

    private bool hasPracticeStarted;

    // Set while the character-set list is being swapped: the bound ComboBox reacts to a new
    // ItemsSource by pushing its own (now stale) SelectedItem back into the view model, which
    // would otherwise overwrite the controller's freshly chosen set.
    private bool suppressSelectedCharacterSetPush;

    private CancellationTokenSource? _practiceTimerCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }
    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenUiSettingsCommand { get; }
    public IAsyncRelayCommand CheckResultCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }
    public IAsyncRelayCommand OpenTrendsCommand { get; }
    public IAsyncRelayCommand OpenConfusionsCommand { get; }
    public IAsyncRelayCommand CheckUpdatesCommand { get; }

    [ObservableProperty]
    private string receivedText = string.Empty;

    [ObservableProperty]
    private string timeCounterText = "00:00";

    [ObservableProperty]
    private StatusLevel timeCounterStatus = StatusLevel.Neutral;

    [ObservableProperty]
    private string[] characterSets = [];

    [ObservableProperty]
    private string selectedCharacterSet = "Default";

    [ObservableProperty]
    private int practiceDuration = 5;

    [ObservableProperty]
    private FontFamily receivedTextFontFamily = FontFamily.Default;

    [ObservableProperty]
    private double receivedTextFontSize = 20.0;

    public MainWindowViewModel(
        IPracticeController practiceController,
        IConfigurationService configurationService,
        IMorseSettingsDialogService settingsDialogService,
        IUiSettingsDialogService uiSettingsDialogService,
        IPracticeResultWindowService practiceResultWindowService,
        IAboutDialogService aboutDialogService,
        ITrendsDialogService trendsDialogService,
        IConfusionsDialogService confusionsDialogService,
        IUpdateChecker updateChecker,
        IInfoDialogService infoDialogService,
        ILogger<MainWindowViewModel> logger)
    {
        _practiceController = practiceController;
        _configurationService = configurationService;
        _settingsDialogService = settingsDialogService;
        _uiSettingsDialogService = uiSettingsDialogService;
        _practiceResultWindowService = practiceResultWindowService;
        _aboutDialogService = aboutDialogService;
        _trendsDialogService = trendsDialogService;
        _confusionsDialogService = confusionsDialogService;
        _updateChecker = updateChecker;
        _infoDialogService = infoDialogService;
        _logger = logger;

        PracticeDuration = _practiceController.PracticeDurationMins;
        RefreshCharacterSets();
        ReceivedTextFontFamily = new FontFamily(_configurationService.Current.UiPreferences.ReceivedTextFontFamily);
        ReceivedTextFontSize = _configurationService.Current.UiPreferences.ReceivedTextFontSize;

        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, CanStartPractice);
        StopPracticeCommand = new RelayCommand(StopPractice, CanStopPractice);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsDialogAsync);
        OpenUiSettingsCommand = new AsyncRelayCommand(OpenUiSettingsDialogAsync);
        CheckResultCommand = new AsyncRelayCommand(OpenResultWindowAsync, CanCheckResult);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        OpenTrendsCommand = new AsyncRelayCommand(OpenTrendsAsync);
        OpenConfusionsCommand = new AsyncRelayCommand(OpenConfusionsAsync);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
        UpdateCommandStates();
    }

    public async Task StartPracticeAsync()
    {
        if (IsPracticeRunning)
        {
            return;
        }

        hasPracticeStarted = true;
        IsPracticeRunning = true;
        UpdateCommandStates();
        ReceivedText = string.Empty;
        TimeCounterText = "Starting practice...";
        TimeCounterStatus = StatusLevel.Info;
        _practiceTimerCancellationTokenSource = new CancellationTokenSource();

        var timerTask = RunPracticeTimerAsync(_practiceTimerCancellationTokenSource.Token);

        try
        {
            await _practiceController.StartAsync();
            TimeCounterText = "Practice completed!";
            TimeCounterStatus = StatusLevel.Success;
            if (_configurationService.Current.UiPreferences.RevealSentTextAfterPractice
                && string.IsNullOrEmpty(ReceivedText))
            {
                var revealedText = _practiceController.LastGeneratedText;
                ReceivedText = _configurationService.Current.UiPreferences.RevealSentTextInLowercase
                    ? revealedText.ToLowerInvariant()
                    : revealedText;
            }
        }
        catch (OperationCanceledException)
        {
            TimeCounterText = "Stopped.";
            TimeCounterStatus = StatusLevel.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Practice session failed unexpectedly");
            TimeCounterText = "Practice failed. Check logs for details.";
            TimeCounterStatus = StatusLevel.Error;
        }
        finally
        {
            if (_practiceTimerCancellationTokenSource != null && !_practiceTimerCancellationTokenSource.IsCancellationRequested)
            {
                _practiceTimerCancellationTokenSource.Cancel();
            }

            try
            {
                await timerTask;
            }
            catch (OperationCanceledException)
            {
            }

            IsPracticeRunning = false;
            UpdateCommandStates();
        }
    }

    public void StopPractice()
    {
        if (!IsPracticeRunning)
        {
            return;
        }

        _practiceController.Stop();

        if (_practiceTimerCancellationTokenSource != null && !_practiceTimerCancellationTokenSource.IsCancellationRequested)
        {
            _practiceTimerCancellationTokenSource.Cancel();
        }

        IsPracticeRunning = false;
        UpdateCommandStates();
        TimeCounterText = "Stopped.";
        TimeCounterStatus = StatusLevel.Error;
    }

    public async Task OpenSettingsDialogAsync()
    {
        var newSettings = await _settingsDialogService.ShowSettingsDialogAsync(_practiceController.CreateSettingsSnapshot());
        if (newSettings is null)
            return;

        if (!_practiceController.TryApplySettings(newSettings, out var error))
        {
            TimeCounterText = error;
            TimeCounterStatus = StatusLevel.Error;
            return;
        }

        RefreshCharacterSets();
        PracticeDuration = _practiceController.PracticeDurationMins;
    }

    public async Task OpenResultWindowAsync()
    {
        var result = _practiceController.BuildResult(ReceivedText);
        var settings = _practiceController.CreateSettingsSnapshot();
        var saved = await _practiceResultWindowService.ShowPracticeResultAsync(
            result,
            settings.Practice.CharacterWpm,
            settings.Practice.AverageWpm,
            _practiceController.IsResultSaved,
            settings.Practice.ErrorThreshold,
            settings.Audio.Noise);
        if (saved)
        {
            _practiceController.IsResultSaved = true;
        }
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

    public async Task OpenUiSettingsDialogAsync()
    {
        var newPrefs = await _uiSettingsDialogService.ShowUiSettingsDialogAsync(
            _configurationService.Current.UiPreferences);
        if (newPrefs is null)
            return;

        _configurationService.Current.UiPreferences = newPrefs.Clone();
        await _configurationService.SaveAsync();
        ReceivedTextFontFamily = new FontFamily(newPrefs.ReceivedTextFontFamily);
        ReceivedTextFontSize = newPrefs.ReceivedTextFontSize;
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

    partial void OnPracticeDurationChanged(int value)
    {
        _practiceController.PracticeDurationMins = value;
    }

    partial void OnReceivedTextChanged(string value)
    {
        UpdateCommandStates();
    }

    private async Task RunPracticeTimerAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        TimeCounterText = "Ready.";
        TimeCounterStatus = StatusLevel.Neutral;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - startedAt;
                TimeCounterText = $"Practicing: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
                TimeCounterStatus = StatusLevel.Info;
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool CanStartPractice()
    {
        return !IsPracticeRunning;
    }

    private bool CanStopPractice()
    {
        return IsPracticeRunning;
    }

    private bool CanCheckResult()
    {
        return !IsPracticeRunning && hasPracticeStarted && !string.IsNullOrEmpty(ReceivedText);
    }

    private void UpdateCommandStates()
    {
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();
        CheckResultCommand.NotifyCanExecuteChanged();
    }
}
