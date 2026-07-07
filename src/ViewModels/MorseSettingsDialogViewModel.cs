using System;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.ViewModels;

public partial class MorseSettingsDialogViewModel : ViewModelBase
{
    private readonly int _defaultDurationMins;
    private readonly string _defaultCharacterSet;
    private readonly IPracticeSettingsValidator _settingsValidator;

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

    partial void OnWpmLockedChanged(bool value)
    {
        if (value)
            AverageWpm = CharacterWpm;
    }

    [ObservableProperty]
    private int selectedSampleRate;

    public int[] SampleRateOptions { get; } = [8000, 11025, 16000, 22050, 32000, 44100, 48000];

    [ObservableProperty]
    private double frequency;

    [ObservableProperty]
    private double volume;

    [ObservableProperty]
    private int beepRampMs;

    [ObservableProperty]
    private double errorThreshold;

    [ObservableProperty]
    private string characterSetsText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public event Action<bool>? CloseRequested;

    public MorseSettingsDialogViewModel(AppConfig config, IPracticeSettingsValidator settingsValidator)
    {
        _settingsValidator = settingsValidator;
        _defaultDurationMins = config.Practice.DefaultDurationMins;
        _defaultCharacterSet = config.Practice.DefaultCharacterSet;

        CharacterWpm = config.Practice.CharacterWpm;
        AverageWpm = config.Practice.AverageWpm;
        WpmLocked = config.Practice.CharacterWpm == config.Practice.AverageWpm;
        SelectedSampleRate = config.Audio.SampleRate;
        Frequency = config.Audio.Frequency;
        Volume = config.Audio.Volume;
        BeepRampMs = config.Audio.BeepRampMs;
        ErrorThreshold = config.Practice.ErrorThreshold;

        if (!SampleRateOptions.Contains(SelectedSampleRate))
        {
            SelectedSampleRate = 44100;
        }

        CharacterSetsText = CharacterSetTextCodec.FormatForEditor(config.CharacterSets);

        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(OnCancel);
    }

    public bool TryBuildSettings(out AppConfig settings)
    {
        settings = new AppConfig();

        if (!CharacterSetTextCodec.TryParse(CharacterSetsText, out var parsedSets, out var parserError))
        {
            ErrorMessage = parserError;
            return false;
        }

        settings = BuildConfig(parsedSets);

        if (!_settingsValidator.TryValidate(settings, out var validationError))
        {
            ErrorMessage = validationError;
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    private void OnSave()
    {
        if (TryBuildSettings(out _))
        {
            CloseRequested?.Invoke(true);
        }
    }

    private void OnCancel()
    {
        CloseRequested?.Invoke(false);
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
                ErrorThreshold = ErrorThreshold,
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
