using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

using PentaGrammata.Services;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly IConfiguration _configuration;

    private readonly int _sampleRate;
    private readonly int _charWpm;
    private readonly int _averageWpm;

    private CancellationTokenSource? _practiceCancellationTokenSource;
    private CancellationTokenSource? _practiceTimerCancellationTokenSource;
    private bool _isPracticing;

    public IAsyncRelayCommand StartPracticeCommand { get; }
    public IRelayCommand StopPracticeCommand { get; }

    public MainWindowViewModel(IMorseGenerator morseGenerator, IMorsePlayer morsePlayer, IConfiguration configuration)
    {
        _morseGenerator = morseGenerator;
        _morsePlayer = morsePlayer;
        _configuration = configuration;

        // Read configuration values with defaults
        _sampleRate = _configuration.GetValue("Audio:SampleRate", 44100);
        _charWpm = _configuration.GetValue("Practice:CharacterWpm", 20);
        _averageWpm = _configuration.GetValue("Practice:AverageWpm", 15);
        PracticeDuration = _configuration.GetValue("Practice:DefaultDuration", 5);

        _practiceCancellationTokenSource = null;
        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, () => !_isPracticing);
        StopPracticeCommand = new RelayCommand(StopPractice, () => _isPracticing);

        // Load character sets from configuration
        var characterSetsSection = _configuration.GetSection("CharacterSets");
        var characterSets = new List<KeyValuePair<string, string>>();
        
        if (characterSetsSection.Exists())
        {
            foreach (var characterSetSection in characterSetsSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(characterSetSection.Value))
                {
                    characterSets.Add(new KeyValuePair<string, string>(characterSetSection.Key, characterSetSection.Value));
                    continue;
                }

                foreach (var child in characterSetSection.GetChildren())
                {
                    if (!string.IsNullOrWhiteSpace(child.Value))
                    {
                        characterSets.Add(new KeyValuePair<string, string>(child.Key, child.Value));
                    }
                }
            }
        }

        if (characterSets.Count == 0)
        {
            characterSets.Add(new KeyValuePair<string, string>("Default", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>"));
        }

        CharacterSets = characterSets;
        var defaultSetName = _configuration.GetValue("Practice:DefaultCharacterSet", "Default");
        SelectedCharacterSet = characterSets.Find(s => s.Key == defaultSetName) is { Key: not "" } match
            ? match
            : CharacterSets[0];
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
        string characterSetCharacters = string.IsNullOrWhiteSpace(SelectedCharacterSet.Value)
            ? CharacterSets[0].Value
            : SelectedCharacterSet.Value;
        
        // 6 characters per group (5 + space)
        int numberOfGroups = PracticeDuration * _averageWpm / 6; 
        string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);
        
        _practiceCancellationTokenSource = new CancellationTokenSource();
        _practiceTimerCancellationTokenSource = new CancellationTokenSource();
        
        _isPracticing = true;
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();

        var timerTask = RunPracticeTimerAsync(_practiceTimerCancellationTokenSource.Token);

        try
        {
            await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: _charWpm, averageWpm: _averageWpm, sampleRate: _sampleRate, _practiceCancellationTokenSource.Token);
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

            _isPracticing = false;
            StartPracticeCommand.NotifyCanExecuteChanged();
            StopPracticeCommand.NotifyCanExecuteChanged();
        }
    }

    public void StopPractice()
    {
        if (_practiceCancellationTokenSource != null && !_practiceCancellationTokenSource.IsCancellationRequested)
        {
            _practiceCancellationTokenSource.Cancel();
        }

        if (_practiceTimerCancellationTokenSource != null && !_practiceTimerCancellationTokenSource.IsCancellationRequested)
        {
            _practiceTimerCancellationTokenSource.Cancel();
        }

        _isPracticing = false;
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();
    }

    private async Task RunPracticeTimerAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        TimeCounterText = "00:00";

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startedAt;
            TimeCounterText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            await Task.Delay(1000, cancellationToken);
        }
    }
}
