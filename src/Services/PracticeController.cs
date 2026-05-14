using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;

namespace PentaGrammata.Services;

public class PracticeController
{
    private const double LengthCorrector = 0.7;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly AppConfig _configuration;
    private readonly string? _userConfigPath;
    private CancellationTokenSource? _cancellationTokenSource;

    public int PracticeDurationMins
    {
        get => _configuration.Practice.DefaultDurationMins;
        set
        {
            if (_configuration.Practice.DefaultDurationMins == value)
                return;

            _configuration.Practice.DefaultDurationMins = value;
            SaveUserConfiguration();
        }
    }

    public List<KeyValuePair<string, string>> CharacterSets { get => _configuration.CharacterSets.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList(); }

    public string SelectedCharacterSet
    {
        get => _configuration.Practice.DefaultCharacterSet;
        set
        {
            if (_configuration.Practice.DefaultCharacterSet == value)
                return;

            _configuration.Practice.DefaultCharacterSet = value;
            SaveUserConfiguration();
        }
    }

    public int SampleRate { get => _configuration.Audio.SampleRate; set => _configuration.Audio.SampleRate = value; }
    public int BeepRampMs { get => _configuration.Audio.BeepRampMs; set => _configuration.Audio.BeepRampMs = value; }
    public int CharacterWpm { get => _configuration.Practice.CharacterWpm; set => _configuration.Practice.CharacterWpm = value; }
    public int AverageWpm { get => _configuration.Practice.AverageWpm; set => _configuration.Practice.AverageWpm = value; }
    public bool IsPracticing { get; private set; }

    public PracticeController()
    {
        var audioPlayer = AudioPlayerFactory.Create();
        _morsePlayer = new MorsePlayer(audioPlayer);
        _morseGenerator = new MorseGenerator();
        _configuration = LoadConfiguration();
        _userConfigPath = ConfigurationPaths.GetPreferredPerUserConfigPath();

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

    private static AppConfig LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        foreach (var userConfigPath in ConfigurationPaths.GetPerUserConfigPaths())
        {
            builder.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
        }

        var configRoot = builder.Build();
        return configRoot.Get<AppConfig>() ?? new AppConfig();
    }

    private void SaveUserConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_userConfigPath))
            return;

        var directory = Path.GetDirectoryName(_userConfigPath);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_configuration, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_userConfigPath, json);
    }
}