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

        CharacterSetsText = CharacterSetTextCodec.FormatForEditor(settings.CharacterSets);
    }

    public bool TryBuildSettings(out PracticeSettings settings)
    {
        settings = new PracticeSettings();

        if (!TryValidateScalarSettings(out var scalarError))
        {
            ErrorMessage = scalarError;
            return false;
        }

        if (!CharacterSetTextCodec.TryParse(CharacterSetsText, out var parsedSets, out var parserError))
        {
            ErrorMessage = parserError;
            return false;
        }

        settings = BuildSettings(parsedSets);

        ErrorMessage = string.Empty;
        return true;
    }

    private bool TryValidateScalarSettings(out string error)
    {
        if (CharacterWpm < 1 || AverageWpm < 1)
        {
            error = "Character and average WPM must be positive values.";
            return false;
        }

        if (AverageWpm > CharacterWpm)
        {
            error = "Average WPM cannot exceed character WPM.";
            return false;
        }

        if (SelectedSampleRate < 8000)
        {
            error = "Sample rate must be at least 8000.";
            return false;
        }

        if (BeepRampMs < 0)
        {
            error = "Beep ramp must be 0 or greater.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private PracticeSettings BuildSettings(IReadOnlyDictionary<string, string> parsedSets)
    {
        var defaultSet = parsedSets.ContainsKey(_defaultCharacterSet)
            ? _defaultCharacterSet
            : parsedSets.Keys.First();

        return new PracticeSettings
        {
            DefaultDurationMins = _defaultDurationMins,
            CharacterWpm = CharacterWpm,
            AverageWpm = AverageWpm,
            SampleRate = SelectedSampleRate,
            BeepRampMs = BeepRampMs,
            DefaultCharacterSet = defaultSet,
            CharacterSets = parsedSets.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal)
        };
    }
}
