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
    public double Frequency { get => _configuration.Audio.Frequency; set => _configuration.Audio.Frequency = value; }
    public double Volume { get => _configuration.Audio.Volume; set => _configuration.Audio.Volume = value; }
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
                string morseCodeToPlay = "vvv = " + morseCode + " <ar>";
                await _morsePlayer.PlayMorseCodeAsync(morseCodeToPlay, charWpm: CharacterWpm, averageWpm: AverageWpm, sampleRate: SampleRate, frequency: Frequency, volume: Volume, beepRampMs: BeepRampMs, _cancellationTokenSource.Token);
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
            ErrorRatePercent = errorRatePercent,
            IsSuccessful = errorRatePercent <= _configuration.Practice.ErrorThreshold
        };
    }

    public AppConfig CreateSettingsSnapshot()
    {
        var characterSets = new CharacterSets();
        foreach (var kv in _configuration.CharacterSets)
            characterSets[kv.Key] = kv.Value;

        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = _configuration.Practice.DefaultDurationMins,
                CharacterWpm = _configuration.Practice.CharacterWpm,
                AverageWpm = _configuration.Practice.AverageWpm,
                DefaultCharacterSet = _configuration.Practice.DefaultCharacterSet,
                ErrorThreshold = _configuration.Practice.ErrorThreshold,
            },
            Audio = new Audio
            {
                SampleRate = _configuration.Audio.SampleRate,
                Frequency = _configuration.Audio.Frequency,
                Volume = _configuration.Audio.Volume,
                BeepRampMs = _configuration.Audio.BeepRampMs,
            },
            CharacterSets = characterSets,
        };
    }

    public bool TryApplySettings(AppConfig settings, out string error)
    {
        if (settings.Practice.DefaultDurationMins < 1)
        {
            error = "Default duration must be at least 1 minute.";
            return false;
        }

        if (settings.Practice.CharacterWpm < 1 || settings.Practice.AverageWpm < 1)
        {
            error = "Character and average WPM must be positive values.";
            return false;
        }

        if (settings.Practice.AverageWpm > settings.Practice.CharacterWpm)
        {
            error = "Average WPM cannot exceed character WPM.";
            return false;
        }

        if (settings.Audio.SampleRate < 8000)
        {
            error = "Sample rate must be at least 8000.";
            return false;
        }

        if (settings.Audio.Frequency <= 0)
        {
            error = "Frequency must be greater than 0.";
            return false;
        }

        if (settings.Audio.Volume < 0 || settings.Audio.Volume > 1)
        {
            error = "Volume must be between 0 and 1.";
            return false;
        }

        if (settings.Audio.BeepRampMs < 0)
        {
            error = "Beep ramp must be 0 or greater.";
            return false;
        }

        if (settings.Practice.ErrorThreshold < 0 || settings.Practice.ErrorThreshold > 100)
        {
            error = "Error rate threshold must be between 0 and 100.";
            return false;
        }

        if (settings.CharacterSets == null || settings.CharacterSets.Count == 0)
        {
            error = "At least one character set is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Practice.DefaultCharacterSet) || !settings.CharacterSets.ContainsKey(settings.Practice.DefaultCharacterSet))
        {
            error = "Default character set must match one of the configured character set names.";
            return false;
        }

        _configuration.Practice.DefaultDurationMins = settings.Practice.DefaultDurationMins;
        _configuration.Practice.CharacterWpm = settings.Practice.CharacterWpm;
        _configuration.Practice.AverageWpm = settings.Practice.AverageWpm;
        _configuration.Audio.SampleRate = settings.Audio.SampleRate;
        _configuration.Audio.Frequency = settings.Audio.Frequency;
        _configuration.Audio.Volume = settings.Audio.Volume;
        _configuration.Audio.BeepRampMs = settings.Audio.BeepRampMs;
        _configuration.Practice.DefaultCharacterSet = settings.Practice.DefaultCharacterSet;
        _configuration.Practice.ErrorThreshold = settings.Practice.ErrorThreshold;

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
        return GetLevenshteinDistance(expected, actual);
    }

    private static string BuildDifferenceText(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(Enumerable.Repeat(".", expected.Length));
        }

        var matrix = BuildLevenshteinMatrix(expected, actual);
        var tokensReversed = new List<string>();

        var i = expected.Length;
        var j = actual.Length;

        var insertedReversed = new StringBuilder();
        var deletedReversed = new StringBuilder();

        void FlushEditBuffers()
        {
            if (insertedReversed.Length > 0)
            {
                var inserted = new string(insertedReversed.ToString().Reverse().ToArray());
                tokensReversed.Add($"[+{inserted}]");
                insertedReversed.Clear();
            }

            if (deletedReversed.Length > 0)
            {
                var deleted = new string(deletedReversed.ToString().Reverse().ToArray());
                tokensReversed.Add($"[-{deleted}]");
                deletedReversed.Clear();
            }
        }

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && AreEqualIgnoreCase(expected[i - 1], actual[j - 1]) && matrix[i, j] == matrix[i - 1, j - 1])
            {
                FlushEditBuffers();
                tokensReversed.Add(".");
                i--;
                j--;
                continue;
            }

            if (i > 0 && j > 0 && matrix[i, j] == matrix[i - 1, j - 1] + 1)
            {
                FlushEditBuffers();
                tokensReversed.Add(expected[i - 1].ToString());
                i--;
                j--;
                continue;
            }

            if (i > 0 && matrix[i, j] == matrix[i - 1, j] + 1)
            {
                deletedReversed.Append(expected[i - 1]);
                i--;
                continue;
            }

            if (j > 0 && matrix[i, j] == matrix[i, j - 1] + 1)
            {
                insertedReversed.Append(actual[j - 1]);
                j--;
                continue;
            }

            // Fallback for unexpected tie/corner cases.
            if (i > 0 && j > 0)
            {
                FlushEditBuffers();
                tokensReversed.Add(expected[i - 1].ToString());
                i--;
                j--;
            }
            else if (i > 0)
            {
                deletedReversed.Append(expected[i - 1]);
                i--;
            }
            else
            {
                insertedReversed.Append(actual[j - 1]);
                j--;
            }
        }

        FlushEditBuffers();
        tokensReversed.Reverse();
        return string.Concat(tokensReversed);
    }

    private static int GetLevenshteinDistance(string expected, string actual)
    {
        var matrix = BuildLevenshteinMatrix(expected, actual);
        return matrix[expected.Length, actual.Length];
    }

    private static int[,] BuildLevenshteinMatrix(string expected, string actual)
    {
        var rows = expected.Length + 1;
        var columns = actual.Length + 1;
        var matrix = new int[rows, columns];

        for (var i = 0; i < rows; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j < columns; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < columns; j++)
            {
                var substitutionCost = AreEqualIgnoreCase(expected[i - 1], actual[j - 1]) ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + substitutionCost);
            }
        }

        return matrix;
    }

    private static bool AreEqualIgnoreCase(char left, char right)
    {
        return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
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