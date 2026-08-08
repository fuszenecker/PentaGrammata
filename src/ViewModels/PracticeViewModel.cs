using System;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

using PentaGrammata.Interfaces;

namespace PentaGrammata.ViewModels;

/// <summary>
/// Owns a single practice session: the running/stopped state, the received-text input, the
/// practice timer, the next-session WPM readout, and the start/stop/check-result commands.
/// Split out of <see cref="MainWindowViewModel"/> so the session lifecycle has a focused,
/// testable home and the shell view model is left with navigation and dialog orchestration.
/// </summary>
public partial class PracticeViewModel : ViewModelBase
{
    private readonly IPracticeController _practiceController;
    private readonly IPracticeResultWindowService _practiceResultWindowService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PracticeViewModel> _logger;

    private bool hasPracticeStarted;

    // Whether the current session's result has already been saved, so reopening the result
    // window for the same session disables the save button. Reset when a new session starts.
    private bool _resultSavedForCurrentSession;

    private CancellationTokenSource? _practiceTimerCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }
    public IAsyncRelayCommand CheckResultCommand { get; }

    [ObservableProperty]
    private bool isPracticeRunning;

    [ObservableProperty]
    private string receivedText = string.Empty;

    [ObservableProperty]
    private string timeCounterText = "00:00";

    [ObservableProperty]
    private StatusLevel timeCounterStatus = StatusLevel.Neutral;

    [ObservableProperty]
    private int practiceDuration = 5;

    // WPM the next session will use. Reflects the in-memory dynamic WPM when auto-adjust is
    // on, otherwise the configured WPM. Refreshed after a session is scored (adjustment) and
    // after settings are applied (reset to configured).
    [ObservableProperty]
    private int nextCharacterWpm;

    [ObservableProperty]
    private int nextAverageWpm;

    public PracticeViewModel(
        IPracticeController practiceController,
        IPracticeResultWindowService practiceResultWindowService,
        IConfigurationService configurationService,
        ILogger<PracticeViewModel> logger)
    {
        _practiceController = practiceController;
        _practiceResultWindowService = practiceResultWindowService;
        _configurationService = configurationService;
        _logger = logger;

        PracticeDuration = _practiceController.PracticeDurationMins;
        RefreshNextWpm();

        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, CanStartPractice);
        StopPracticeCommand = new RelayCommand(StopPractice, CanStopPractice);
        CheckResultCommand = new AsyncRelayCommand(OpenResultWindowAsync, CanCheckResult);
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
        _resultSavedForCurrentSession = false;
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

    public async Task OpenResultWindowAsync()
    {
        var result = _practiceController.BuildResult(ReceivedText);
        var settings = _practiceController.CreateSettingsSnapshot();
        // The WPM passed to the result window is the one actually used during the session
        // (the dynamic WPM when auto-adjust is on), not the configured starting point, so
        // the displayed values and any saved statistics record reflect reality.
        var saved = await _practiceResultWindowService.ShowPracticeResultAsync(
            result,
            _practiceController.LastUsedCharacterWpm,
            _practiceController.LastUsedAverageWpm,
            _resultSavedForCurrentSession,
            settings.Practice.ErrorThreshold,
            settings.Audio.Noise);
        // BuildResult may have adjusted the dynamic WPM; refresh the status-bar readout.
        RefreshNextWpm();
        if (saved)
        {
            _resultSavedForCurrentSession = true;
        }
    }

    /// <summary>
    /// Re-reads the next-session WPM from the controller so the status bar reflects the
    /// current dynamic WPM (after an adjustment or a settings reset).
    /// </summary>
    private void RefreshNextWpm()
    {
        NextCharacterWpm = _practiceController.CurrentCharacterWpm;
        NextAverageWpm = _practiceController.CurrentAverageWpm;
    }

    /// <summary>
    /// Called by the shell after settings are applied: refreshes the practice-duration value
    /// and the next-session WPM readout (applying settings resets the dynamic WPM to the
    /// configured values).
    /// </summary>
    public void RefreshFromAppliedSettings()
    {
        PracticeDuration = _practiceController.PracticeDurationMins;
        RefreshNextWpm();
    }

    /// <summary>
    /// Shows a transient status message on the practice status bar. Used by the shell to
    /// surface settings-apply errors without the practice view model owning the settings dialog.
    /// </summary>
    public void DisplayStatusMessage(string text, StatusLevel level)
    {
        TimeCounterText = text;
        TimeCounterStatus = level;
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
