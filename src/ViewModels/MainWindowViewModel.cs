using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Services;
using PentaGrammata.Views;
using Avalonia.Controls;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PracticeController _practiceController;
    private bool _isPracticeRunning;

    private CancellationTokenSource? _practiceTimerCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }

    [ObservableProperty]
    private string greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string timeCounterText = "00:00";

    [ObservableProperty]
    private string[] characterSets = [];

    [ObservableProperty]
    private string selectedCharacterSet = "Default";

    [ObservableProperty]
    private int practiceDuration = 5;

    public MainWindowViewModel(PracticeController practiceController)
    {
        _practiceController = practiceController;

        PracticeDuration = _practiceController.PracticeDurationMins;
        CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        SelectedCharacterSet = _practiceController.SelectedCharacterSet;

        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, CanStartPractice);
        StopPracticeCommand = new RelayCommand(StopPractice, CanStopPractice);
        UpdateCommandStates();
    }

    public async Task StartPracticeAsync()
    {
        if (_isPracticeRunning)
        {
            return;
        }

        _isPracticeRunning = true;
        UpdateCommandStates();
        TimeCounterText = "Starting practice...";
        _practiceTimerCancellationTokenSource = new CancellationTokenSource();

        var timerTask = RunPracticeTimerAsync(_practiceTimerCancellationTokenSource.Token);

        try
        {
            await _practiceController.StartAsync();
            TimeCounterText = "Practice completed!";
        }
        catch (Exception ex)
        {
            TimeCounterText = $"Error: {ex.Message}";
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

            _isPracticeRunning = false;
            UpdateCommandStates();
        }
    }

    public void StopPractice()
    {
        if (!_isPracticeRunning)
        {
            return;
        }

        _practiceController.Stop();

        if (_practiceTimerCancellationTokenSource != null && !_practiceTimerCancellationTokenSource.IsCancellationRequested)
        {
            _practiceTimerCancellationTokenSource.Cancel();
        }

        _isPracticeRunning = false;
        UpdateCommandStates();
        TimeCounterText = "Stopped.";
    }

    public async Task OpenSettingsDialogAsync(Window owner)
    {
        var settingsDialogViewModel = new SettingsDialogViewModel(_practiceController.CreateSettingsSnapshot());
        var settingsDialog = new SettingsDialog
        {
            DataContext = settingsDialogViewModel
        };

        var result = await settingsDialog.ShowDialog<bool>(owner);
        if (!result)
            return;

        if (!settingsDialogViewModel.TryBuildSettings(out var newSettings))
            return;

        if (!_practiceController.TryApplySettings(newSettings, out var error))
        {
            TimeCounterText = error;
            return;
        }

        CharacterSets = [.. _practiceController.CharacterSets.Select(x => x.Key)];
        SelectedCharacterSet = _practiceController.SelectedCharacterSet;
        PracticeDuration = _practiceController.PracticeDurationMins;
    }

    public void OpenResultWindow(Window owner)
    {
        var result = _practiceController.BuildResult(Greeting);
        var resultWindow = new PracticeResultWindow
        {
            DataContext = new PracticeResultWindowViewModel(result)
        };

        resultWindow.Show(owner);
    }

    partial void OnSelectedCharacterSetChanged(string value)
    {
        _practiceController.SelectedCharacterSet = value;
    }

    partial void OnPracticeDurationChanged(int value)
    {
        _practiceController.PracticeDurationMins = value;
    }

    private async Task RunPracticeTimerAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        TimeCounterText = "Ready.";

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - startedAt;
                TimeCounterText = $"Practicing: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool CanStartPractice()
    {
        return !_isPracticeRunning;
    }

    private bool CanStopPractice()
    {
        return _isPracticeRunning;
    }

    private void UpdateCommandStates()
    {
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();
    }
}
