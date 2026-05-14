using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

public class PracticeController
{
    private const double LengthCorrector = 0.7;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly AppConfig _configuration;
    private readonly string? _userConfigPath;
    private CancellationTokenSource? _cancellationTokenSource;

    public string LastGeneratedText { get; private set; } = string.Empty;
    public string LastReceivedText { get; private set; } = string.Empty;

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
        System.Diagnostics.Debug.WriteLine($"StartAsync called on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;
        System.Diagnostics.Debug.WriteLine($"IsPracticing set to true");

        try
        {
            string characterSetCharacters = _configuration.CharacterSets.TryGetValue(SelectedCharacterSet, out var selectedCharacters)
                && !string.IsNullOrWhiteSpace(selectedCharacters)
                    ? selectedCharacters
                    : CharacterSets.FirstOrDefault().Value;

            if (string.IsNullOrWhiteSpace(characterSetCharacters))
                characterSetCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

            int numberOfGroups = (int)(PracticeDurationMins * AverageWpm * LengthCorrector);
            System.Diagnostics.Debug.WriteLine($"Generating {numberOfGroups} morse groups");
            
            string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);
            LastGeneratedText = morseCode;
            System.Diagnostics.Debug.WriteLine($"Generated morse code, about to play audio on thread {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            try
            {
                await _morsePlayer.PlayMorseCodeAsync(morseCode, charWpm: CharacterWpm, averageWpm: AverageWpm, sampleRate: SampleRate, beepRampMs: BeepRampMs, _cancellationTokenSource.Token);
                System.Diagnostics.Debug.WriteLine("Audio playback completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio playback error: {ex}");
                throw;
            }
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine($"StartAsync finally block: setting IsPracticing to false");
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

    public PracticeResult BuildResult(string receivedText)
    {
        LastReceivedText = receivedText ?? string.Empty;

        var sentGroups = SplitGroups(LastGeneratedText);
        var receivedGroups = SplitGroups(LastReceivedText);

        var rowCount = Math.Max(sentGroups.Count, receivedGroups.Count);
        var rows = new List<PracticeResultRow>(rowCount);

        var characterCount = sentGroups.Sum(x => x.Length);
        var errorCount = 0;

        for (var i = 0; i < rowCount; i++)
        {
            var sent = i < sentGroups.Count ? sentGroups[i] : string.Empty;
            var received = i < receivedGroups.Count ? receivedGroups[i] : string.Empty;

            var groupErrors = CountGroupErrors(sent, received);
            errorCount += groupErrors;

            rows.Add(new PracticeResultRow
            {
                SentGroup = sent,
                ReceivedGroup = received,
                Difference = BuildDifferenceText(sent, received)
            });
        }

        var errorRatePercent = characterCount > 0
            ? (double)errorCount / characterCount * 100d
            : 0d;

        return new PracticeResult
        {
            Rows = rows,
            CharacterCount = characterCount,
            ErrorCount = errorCount,
            ErrorRatePercent = errorRatePercent
        };
    }

    public PracticeSettings CreateSettingsSnapshot()
    {
        return new PracticeSettings
        {
            DefaultDurationMins = _configuration.Practice.DefaultDurationMins,
            CharacterWpm = _configuration.Practice.CharacterWpm,
            AverageWpm = _configuration.Practice.AverageWpm,
            SampleRate = _configuration.Audio.SampleRate,
            BeepRampMs = _configuration.Audio.BeepRampMs,
            DefaultCharacterSet = _configuration.Practice.DefaultCharacterSet,
            CharacterSets = _configuration.CharacterSets.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    public bool TryApplySettings(PracticeSettings settings, out string error)
    {
        if (settings.DefaultDurationMins < 1)
        {
            error = "Default duration must be at least 1 minute.";
            return false;
        }

        if (settings.CharacterWpm < 1 || settings.AverageWpm < 1)
        {
            error = "Character and average WPM must be positive values.";
            return false;
        }

        if (settings.AverageWpm > settings.CharacterWpm)
        {
            error = "Average WPM cannot exceed character WPM.";
            return false;
        }

        if (settings.SampleRate < 8000)
        {
            error = "Sample rate must be at least 8000.";
            return false;
        }

        if (settings.BeepRampMs < 0)
        {
            error = "Beep ramp must be 0 or greater.";
            return false;
        }

        if (settings.CharacterSets == null || settings.CharacterSets.Count == 0)
        {
            error = "At least one character set is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultCharacterSet) || !settings.CharacterSets.ContainsKey(settings.DefaultCharacterSet))
        {
            error = "Default character set must match one of the configured character set names.";
            return false;
        }

        _configuration.Practice.DefaultDurationMins = settings.DefaultDurationMins;
        _configuration.Practice.CharacterWpm = settings.CharacterWpm;
        _configuration.Practice.AverageWpm = settings.AverageWpm;
        _configuration.Audio.SampleRate = settings.SampleRate;
        _configuration.Audio.BeepRampMs = settings.BeepRampMs;
        _configuration.Practice.DefaultCharacterSet = settings.DefaultCharacterSet;

        _configuration.CharacterSets.Clear();
        foreach (var item in settings.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                _configuration.CharacterSets[item.Key] = item.Value;
            }
        }

        SaveUserConfiguration();
        error = string.Empty;
        return true;
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

    private static List<string> SplitGroups(string text)
    {
        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static int CountGroupErrors(string expected, string actual)
    {
        var maxLength = Math.Max(expected.Length, actual.Length);
        var errors = 0;

        for (var i = 0; i < maxLength; i++)
        {
            var expectedChar = i < expected.Length ? expected[i] : '\0';
            var actualChar = i < actual.Length ? actual[i] : '\0';

            if (expectedChar != actualChar)
            {
                errors++;
            }
        }

        return errors;
    }

    private static string BuildDifferenceText(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return string.Concat(Enumerable.Repeat(".", expected.Length));
        }

        var charMarks = new StringBuilder();

        // Compare up to the length of the expected string
        for (var i = 0; i < expected.Length; i++)
        {
            if (i < actual.Length && expected[i] == actual[i])
            {
                charMarks.Append('.');
            }
            else
            {
                charMarks.Append('X');
            }
        }

        var result = charMarks.ToString();

        // Handle missing characters
        if (actual.Length < expected.Length)
        {
            result += $" [-{expected.Length - actual.Length}]";
        }

        // Handle inserted characters
        if (actual.Length > expected.Length)
        {
            var extras = actual.Substring(expected.Length);
            result += $" [+{extras}]";
        }

        return result;
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