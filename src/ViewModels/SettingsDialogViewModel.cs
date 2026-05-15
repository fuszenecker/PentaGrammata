using System;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;

namespace PentaGrammata.ViewModels;

public partial class SettingsDialogViewModel : ViewModelBase
{
    private readonly int _defaultDurationMins;
    private readonly string _defaultCharacterSet;

    [ObservableProperty]
    private int characterWpm;

    partial void OnCharacterWpmChanged(int value)
    {
        if (WpmLocked)
            AverageWpm = value;
        else if (AverageWpm > value)
            AverageWpm = value;
    }

    [ObservableProperty]
    private int averageWpm;

    partial void OnAverageWpmChanged(int value)
    {
        if (WpmLocked)
            CharacterWpm = value;
    }

    [ObservableProperty]
    private bool wpmLocked;

    [ObservableProperty]
    private int selectedSampleRate;

    public int[] SampleRateOptions { get; } = [8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 192000];

    [ObservableProperty]
    private double frequency;

    [ObservableProperty]
    private double volume;

    [ObservableProperty]
    private int beepRampMs;

    [ObservableProperty]
    private string characterSetsText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public SettingsDialogViewModel(AppConfig config)
    {
        _defaultDurationMins = config.Practice.DefaultDurationMins;
        _defaultCharacterSet = config.Practice.DefaultCharacterSet;

        CharacterWpm = config.Practice.CharacterWpm;
        AverageWpm = config.Practice.AverageWpm;
        WpmLocked = config.Practice.CharacterWpm == config.Practice.AverageWpm;
        SelectedSampleRate = config.Audio.SampleRate;
        Frequency = config.Audio.Frequency;
        Volume = config.Audio.Volume;
        BeepRampMs = config.Audio.BeepRampMs;

        if (!SampleRateOptions.Contains(SelectedSampleRate))
        {
            SelectedSampleRate = 44100;
        }

        CharacterSetsText = CharacterSetTextCodec.FormatForEditor(config.CharacterSets);
    }

    public bool TryBuildSettings(out AppConfig settings)
    {
        settings = new AppConfig();

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

        settings = BuildConfig(parsedSets);

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

        if (Frequency <= 0)
        {
            error = "Frequency must be greater than 0.";
            return false;
        }

        if (Volume < 0 || Volume > 1)
        {
            error = "Volume must be between 0 and 1.";
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

    private AppConfig BuildConfig(IReadOnlyDictionary<string, string> parsedSets)
    {
        var defaultSet = parsedSets.ContainsKey(_defaultCharacterSet)
            ? _defaultCharacterSet
            : parsedSets.Keys.First();

        var characterSets = new CharacterSets();
        foreach (var kv in parsedSets)
            characterSets[kv.Key] = kv.Value;

        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = _defaultDurationMins,
                CharacterWpm = CharacterWpm,
                AverageWpm = AverageWpm,
                DefaultCharacterSet = defaultSet,
            },
            Audio = new Audio
            {
                SampleRate = SelectedSampleRate,
                Frequency = Frequency,
                Volume = Volume,
                BeepRampMs = BeepRampMs,
            },
            CharacterSets = characterSets,
        };
    }
}
