using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Services;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PracticeController _practiceController;

    private CancellationTokenSource? _practiceTimerCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }

    public MainWindowViewModel(PracticeController practiceController)
    {
        _practiceController = practiceController;

        PracticeDuration = _practiceController.PracticeDurationMins;
        CharacterSets = _practiceController.CharacterSets;
        SelectedCharacterSet = _practiceController.SelectedCharacterSet;

        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, () => !_practiceController.IsPracticing);
        StopPracticeCommand = new RelayCommand(StopPractice, () => _practiceController.IsPracticing);
    }

    [ObservableProperty]
    private string greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string timeCounterText = "00:00";

    [ObservableProperty]
    private List<KeyValuePair<string, string>> characterSets;

    [ObservableProperty]
    private KeyValuePair<string, string> selectedCharacterSet;

    [ObservableProperty]
    private int practiceDuration = 5;

    private async Task StartPracticeAsync()
    {
        _practiceController.SelectedCharacterSet = SelectedCharacterSet;
        _practiceController.PracticeDurationMins = PracticeDuration;

        _practiceTimerCancellationTokenSource = new CancellationTokenSource();

        var timerTask = RunPracticeTimerAsync(_practiceTimerCancellationTokenSource.Token);

        // StartAsync synchronously sets IsPracticing = true before its first await,
        // so we notify after starting it to reflect the correct state.
        var practiceTask = _practiceController.StartAsync();
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();

        try
        {
            await practiceTask;
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

            StartPracticeCommand.NotifyCanExecuteChanged();
            StopPracticeCommand.NotifyCanExecuteChanged();
        }
    }

    public void StopPractice()
    {
        _practiceController.Stop();

        if (_practiceTimerCancellationTokenSource != null && !_practiceTimerCancellationTokenSource.IsCancellationRequested)
        {
            _practiceTimerCancellationTokenSource.Cancel();
        }

        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();
    }

    private async Task RunPracticeTimerAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        TimeCounterText = "Ready.";

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startedAt;
            TimeCounterText = $"Practicing: {(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            await Task.Delay(1000, cancellationToken);
        }

        TimeCounterText = "Stopped.";
    }
}
