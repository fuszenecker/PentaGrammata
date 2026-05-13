using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Services;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;

    private CancellationTokenSource? _practiceCancellationTokenSource;

    public IAsyncRelayCommand StartPracticeCommand { get; }

    public MainWindowViewModel(IMorseGenerator morseGenerator, IMorsePlayer morsePlayer)
    {
        _morseGenerator = morseGenerator;
        _morsePlayer = morsePlayer;
        _practiceCancellationTokenSource = null;
        StartPracticeCommand = new AsyncRelayCommand(StartPracticeAsync);

        CharacterPalettes = new List<string>
        {
            "Palette 1",
            "Palette 2",
            "Palette 3"
        };
    }

    [ObservableProperty]
    private string greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private List<string> characterPalettes;

    [ObservableProperty]
    private int practiceDuration = 5;

    private async Task StartPracticeAsync()
    {
        StatusText = "Practice started!";
        string morseCode = _morseGenerator.GenerateGroupsOf5("Hello World", 3);
        _practiceCancellationTokenSource = new CancellationTokenSource();
        await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: 20, textWpm: 15, sampleRate: 44100, _practiceCancellationTokenSource.Token);
    }
}
