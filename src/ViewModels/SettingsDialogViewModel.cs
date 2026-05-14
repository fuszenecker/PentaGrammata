using System;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using PentaGrammata.Configuration;

namespace PentaGrammata.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly int _defaultDurationMins;
    private readonly string _defaultCharacterSet;

    [ObservableProperty]
    private int characterWpm;

    [ObservableProperty]
    private int averageWpm;

    [ObservableProperty]
    private int selectedSampleRate;

    public int[] SampleRateOptions { get; } = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 192000];

    [ObservableProperty]
    private int beepRampMs;

    [ObservableProperty]
    private string characterSetsText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public SettingsDialogViewModel(PracticeSettings settings)
    {
        _defaultDurationMins = settings.DefaultDurationMins;
        _defaultCharacterSet = settings.DefaultCharacterSet;

        CharacterWpm = settings.CharacterWpm;
        AverageWpm = settings.AverageWpm;
        SelectedSampleRate = settings.SampleRate;
        BeepRampMs = settings.BeepRampMs;

        if (!SampleRateOptions.Contains(SelectedSampleRate))
        {
            SelectedSampleRate = 44100;
        }

        CharacterSetsText = string.Join(Environment.NewLine,
            settings.CharacterSets
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => $"{kv.Key}={kv.Value}"));
    }

    public bool TryBuildSettings(out PracticeSettings settings)
    {
        settings = new PracticeSettings();

        if (CharacterWpm < 1 || AverageWpm < 1)
        {
            ErrorMessage = "Character and average WPM must be positive values.";
            return false;
        }

        if (AverageWpm > CharacterWpm)
        {
            ErrorMessage = "Average WPM cannot exceed character WPM.";
            return false;
        }

        if (SelectedSampleRate < 8000)
        {
            ErrorMessage = "Sample rate must be at least 8000.";
            return false;
        }

        if (BeepRampMs < 0)
        {
            ErrorMessage = "Beep ramp must be 0 or greater.";
            return false;
        }

        var parsedSets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = CharacterSetsText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            {
                ErrorMessage = "Character set lines must use Name=Value format.";
                return false;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                ErrorMessage = "Character set name and value cannot be empty.";
                return false;
            }

            parsedSets[name] = value;
        }

        if (parsedSets.Count == 0)
        {
            ErrorMessage = "At least one character set is required.";
            return false;
        }

        var defaultSet = parsedSets.ContainsKey(_defaultCharacterSet)
            ? _defaultCharacterSet
            : parsedSets.Keys.First();

        settings = new PracticeSettings
        {
            DefaultDurationMins = _defaultDurationMins,
            CharacterWpm = CharacterWpm,
            AverageWpm = AverageWpm,
            SampleRate = SelectedSampleRate,
            BeepRampMs = BeepRampMs,
            DefaultCharacterSet = defaultSet,
            CharacterSets = parsedSets.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)
        };

        ErrorMessage = string.Empty;
        return true;
    }
}
