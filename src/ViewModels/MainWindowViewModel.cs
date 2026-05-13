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
    private readonly int _textWpm;

    private CancellationTokenSource? _practiceCancellationTokenSource;
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
        _textWpm = _configuration.GetValue("Practice:TextWpm", 15);
        PracticeDuration = _configuration.GetValue("Practice:DefaultDuration", 5);

        _practiceCancellationTokenSource = null;
        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync, () => !_isPracticing);
        StopPracticeCommand = new RelayCommand(StopPractice, () => _isPracticing);

        // Load character palettes from configuration
        var palettesSection = _configuration.GetSection("CharacterPalettes");
        var palettes = new List<KeyValuePair<string, string>>();
        
        if (palettesSection.Exists())
        {
            foreach (var paletteSection in palettesSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(paletteSection.Value))
                {
                    palettes.Add(new KeyValuePair<string, string>(paletteSection.Key, paletteSection.Value));
                    continue;
                }

                foreach (var child in paletteSection.GetChildren())
                {
                    if (!string.IsNullOrWhiteSpace(child.Value))
                        palettes.Add(new KeyValuePair<string, string>(child.Key, child.Value));
                }
            }
        }

        if (palettes.Count == 0)
        {
            palettes.Add(new KeyValuePair<string, string>("Default", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>"));
        }

        CharacterPalettes = palettes;
        SelectedCharacterPalette = CharacterPalettes[0];
    }

    [ObservableProperty]
    private string greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private List<KeyValuePair<string, string>> characterPalettes;

    [ObservableProperty]
    private KeyValuePair<string, string> selectedCharacterPalette;

    [ObservableProperty]
    private int practiceDuration = 5;

    private async Task StartPracticeAsync()
    {
        StatusText = "Practice started!";
        string paletteCharacters = string.IsNullOrWhiteSpace(SelectedCharacterPalette.Value)
            ? CharacterPalettes[0].Value
            : SelectedCharacterPalette.Value;
        string morseCode = _morseGenerator.GenerateGroupsOf5(paletteCharacters, 3);
        _practiceCancellationTokenSource = new CancellationTokenSource();
        _isPracticing = true;
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();

        try
        {
            await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: _charWpm, textWpm: _textWpm, sampleRate: _sampleRate, _practiceCancellationTokenSource.Token);
        }
        finally
        {
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
            StatusText = "Practice stopped.";
        }

        _isPracticing = false;
        StartPracticeCommand.NotifyCanExecuteChanged();
        StopPracticeCommand.NotifyCanExecuteChanged();
    }
}
