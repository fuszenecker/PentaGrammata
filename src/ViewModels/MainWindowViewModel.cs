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
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private bool isPracticeRunning;

    private bool hasPracticeStarted;

    private CancellationTokenSource? _practiceTimerCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }
    public IAsyncRelayCommand OpenSettingsCommand { get; }
    public IAsyncRelayCommand OpenUiSettingsCommand { get; }
    public IAsyncRelayCommand CheckResultCommand { get; }
    public IAsyncRelayCommand OpenAboutCommand { get; }

    [ObservableProperty]
    private string receivedText = string.Empty;

    [ObservableProperty]
    private string timeCounterText = "00:00";

    [ObservableProperty]
    private IBrush timeCounterForeground = Brushes.Gainsboro;

    [ObservableProperty]
    private string[] characterSets = [];

    [ObservableProperty]
    private string selectedCharacterSet = "Default";

    [ObservableProperty]
    private int practiceDuration = 5;

    [ObservableProperty]
    private double receivedTextFontSize = 24.0;

    public MainWindowViewModel(
        IPracticeController practiceController,
        IConfigurationService configurationService,
        IMorseSettingsDialogService settingsDialogService,
        IUiSettingsDialogService uiSettingsDialogService,
        IPracticeResultWindowService practiceResultWindowService,
        IAboutDialogService aboutDialogService,
        ILogger<MainWindowViewModel> logger)
    {
        _practiceController = practiceController;
        _configurationService = configurationService;
        _settingsDialogService = settingsDialogService;
        _uiSettingsDialogService = uiSettingsDialogService;
        _practiceResultWindowService = practiceResultWindowService;
        _aboutDialogService = aboutDialogService;
        _logger = logger;

        PracticeDuration = _practiceController.PracticeDurationMins;
        CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        SelectedCharacterSet = _practiceController.SelectedCharacterSet;
        ReceivedTextFontSize = _configurationService.Current.UiPreferences.ReceivedTextFontSize;

        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, CanStartPractice);
        StopPracticeCommand = new RelayCommand(StopPractice, CanStopPractice);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsDialogAsync);
        OpenUiSettingsCommand = new AsyncRelayCommand(OpenUiSettingsDialogAsync);
        CheckResultCommand = new AsyncRelayCommand(OpenResultWindowAsync, CanCheckResult);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
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
        TimeCounterForeground = Brushes.CornflowerBlue;
        _practiceTimerCancellationTokenSource = new CancellationTokenSource();

        var timerTask = RunPracticeTimerAsync(_practiceTimerCancellationTokenSource.Token);

        try
        {
            await _practiceController.StartAsync();
            TimeCounterText = "Practice completed!";
            TimeCounterForeground = Brushes.LimeGreen;
            if (_configurationService.Current.UiPreferences.RevealSentTextAfterPractice
                && string.IsNullOrEmpty(ReceivedText))
            {
                ReceivedText = _practiceController.LastGeneratedText;
            }
        }
        catch (OperationCanceledException)
        {
            TimeCounterText = "Stopped.";
            TimeCounterForeground = Brushes.IndianRed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Practice session failed unexpectedly");
            TimeCounterText = "Practice failed. Check logs for details.";
            TimeCounterForeground = Brushes.IndianRed;
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
        TimeCounterForeground = Brushes.IndianRed;
    }

    public async Task OpenSettingsDialogAsync()
    {
        var newSettings = await _settingsDialogService.ShowSettingsDialogAsync(_practiceController.CreateSettingsSnapshot());
        if (newSettings is null)
            return;

        if (!_practiceController.TryApplySettings(newSettings, out var error))
        {
            TimeCounterText = error;
            TimeCounterForeground = Brushes.IndianRed;
            return;
        }

        CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        SelectedCharacterSet = _practiceController.SelectedCharacterSet;
        PracticeDuration = _practiceController.PracticeDurationMins;
    }

    public async Task OpenResultWindowAsync()
    {
        var result = _practiceController.BuildResult(ReceivedText);
        var settings = _practiceController.CreateSettingsSnapshot();
        var saved = await _practiceResultWindowService.ShowPracticeResultAsync(
            result, settings.Practice.CharacterWpm, settings.Practice.AverageWpm, _practiceController.IsResultSaved);
        if (saved)
        {
            _practiceController.IsResultSaved = true;
        }
    }

    public Task OpenAboutAsync()
    {
        return _aboutDialogService.ShowAboutAsync();
    }

    public async Task OpenUiSettingsDialogAsync()
    {
        var newPrefs = await _uiSettingsDialogService.ShowUiSettingsDialogAsync(
            _configurationService.Current.UiPreferences);
        if (newPrefs is null)
            return;

        _configurationService.Current.UiPreferences.ReceivedTextFontSize = newPrefs.ReceivedTextFontSize;
        _configurationService.Current.UiPreferences.RevealSentTextAfterPractice = newPrefs.RevealSentTextAfterPractice;
        _configurationService.Current.UiPreferences.SuppressedDialogs = [.. newPrefs.SuppressedDialogs];
        await _configurationService.SaveAsync();
        ReceivedTextFontSize = newPrefs.ReceivedTextFontSize;
    }

    partial void OnSelectedCharacterSetChanged(string value)
    {
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
        TimeCounterForeground = Brushes.Gainsboro;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - startedAt;
                TimeCounterText = $"Practicing: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
                TimeCounterForeground = Brushes.CornflowerBlue;
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
