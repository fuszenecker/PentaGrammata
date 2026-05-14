using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;

namespace PentaGrammata.Services;

public class PracticeController
{
    private const double LengthCorrector = 0.7;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly AppConfig _configuration;
    private CancellationTokenSource? _cancellationTokenSource;

    public int PracticeDurationMins { get => _configuration.Practice.DefaultDurationMins; set => _configuration.Practice.DefaultDurationMins = value; }
    public List<KeyValuePair<string, string>> CharacterSets { get => _configuration.CharacterSets.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList(); }
    public string SelectedCharacterSet { get; set; }

    public int SampleRate { get => _configuration.Audio.SampleRate; set => _configuration.Audio.SampleRate = value; }
    public int BeepRampMs { get => _configuration.Audio.BeepRampMs; set => _configuration.Audio.BeepRampMs = value; }
    public int CharacterWpm { get => _configuration.Practice.CharacterWpm; set => _configuration.Practice.CharacterWpm = value; }
    public int AverageWpm { get => _configuration.Practice.AverageWpm; set => _configuration.Practice.AverageWpm = value; }
    public bool IsPracticing { get; private set; }

    public PracticeController(AppConfig configuration)
    {
        var audioPlayer = AudioPlayerFactory.Create();
        _morsePlayer = new MorsePlayer(audioPlayer);
        _morseGenerator = new MorseGenerator();
        _configuration = configuration;

        SampleRate = _configuration.Audio.SampleRate;
        BeepRampMs = _configuration.Audio.BeepRampMs;
        CharacterWpm = _configuration.Practice.CharacterWpm;
        AverageWpm = _configuration.Practice.AverageWpm;
        PracticeDurationMins = _configuration.Practice.DefaultDurationMins;

        var characterSets = new List<KeyValuePair<string, string>>();

        foreach (var characterSet in _configuration.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(characterSet.Value))
                characterSets.Add(new KeyValuePair<string, string>(characterSet.Key, characterSet.Value));
        }

        if (characterSets.Count == 0)
        {
            characterSets.Add(new KeyValuePair<string, string>("Default", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>"));
        }

        SelectedCharacterSet = _configuration.Practice.DefaultCharacterSet ?? characterSets?[0].Key ?? "Default";
    }

    public async Task StartAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;

        try
        {
            string characterSetCharacters = _configuration.CharacterSets.TryGetValue(SelectedCharacterSet, out var selectedCharacters)
                && !string.IsNullOrWhiteSpace(selectedCharacters)
                    ? selectedCharacters
                    : CharacterSets.FirstOrDefault().Value;

            if (string.IsNullOrWhiteSpace(characterSetCharacters))
                characterSetCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

            int numberOfGroups = (int)(PracticeDurationMins * AverageWpm * LengthCorrector);
            string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);

            await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: CharacterWpm, averageWpm: AverageWpm, sampleRate: SampleRate, beepRampMs: BeepRampMs, _cancellationTokenSource.Token);
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