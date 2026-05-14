using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PentaGrammata.Services;

public class PracticeController
{
    private const double LengthCorrector = 3.0/4.0;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly IConfiguration _configuration;

    public PracticeController(IConfiguration configuration)
    {
        var audioPlayer = AudioPlayerFactory.Create();
        _morsePlayer = new MorsePlayer(audioPlayer);
        _morseGenerator = new MorseGenerator();
        _configuration = configuration;

                // Read configuration values with defaults
        SampleRate = _configuration.GetValue("Audio:SampleRate", 44100);
        CharacterWpm = _configuration.GetValue("Practice:CharacterWpm", 20);
        AverageWpm = _configuration.GetValue("Practice:AverageWpm", 15);
        PracticeDuration = _configuration.GetValue("Practice:DefaultDuration", 5);

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

    public int PracticeDuration { get; set; }
    public List<KeyValuePair<string, string>> CharacterSets { get; }
    public KeyValuePair<string, string> SelectedCharacterSet { get; set; }
    public int SampleRate { get; }
    public int CharacterWpm { get; }
    public int AverageWpm { get; }

    private CancellationTokenSource? _cancellationTokenSource;

    public bool IsPracticing { get; private set; }

    public async Task StartAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;

        try
        {
            string characterSetCharacters = string.IsNullOrWhiteSpace(SelectedCharacterSet.Value)
                ? CharacterSets[0].Value
                : SelectedCharacterSet.Value;

            int numberOfGroups = (int)(PracticeDuration * AverageWpm * LengthCorrector);
            string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);

            await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: CharacterWpm, averageWpm: AverageWpm, sampleRate: SampleRate, _cancellationTokenSource.Token);
        }
        finally
        {
            IsPracticing = false;
        }
    }

    public void Stop()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }
}