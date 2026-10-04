using System;
using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public partial class MorseSettingsDialogViewModel : ViewModelBase
{
    /// <summary>
    /// Properties that feed the audio signal chain: changing any of them can change how close
    /// playback runs to full scale, so the distortion warning is recomputed.
    /// </summary>
    private static readonly HashSet<string> AudioChainPropertyNames =
    [
        nameof(CharacterWpm),
        nameof(AverageWpm),
        nameof(SelectedSampleRate),
        nameof(Frequency),
        nameof(VolumeDb),
        nameof(BeepRampMs),
        nameof(SelectedNoiseType),
        nameof(NoiseSnrDb),
        nameof(NoiseBandwidthHz),
        nameof(AgcEnabled),
        nameof(AgcDelaySeconds),
        nameof(AgcMaxGainDb),
        nameof(ApfEnabled),
        nameof(ApfBandwidthHz),
        nameof(ApfPeakGainDb),
        nameof(QsbEnabled),
        nameof(QsbDepthDb),
        nameof(QsbPeriodSeconds),
    ];

    private readonly int _defaultDurationMins;
    private readonly string _defaultCharacterSet;
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IAudioHeadroomAnalyzer _headroomAnalyzer;
    private readonly ICharacterSetTextCodec _characterSetTextCodec;

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
    private double volumeDb;

    [ObservableProperty]
    private int beepRampMs;

    [ObservableProperty]
    private NoiseType selectedNoiseType;

    public NoiseType[] NoiseTypeOptions { get; } = [NoiseType.None, NoiseType.Gaussian, NoiseType.Uniform, NoiseType.Pink];

    // Shown to the user as a signal-to-noise ratio (higher = cleaner). Stored config keeps
    // the noise level relative to the CW signal, which is the negation: SNR = -LevelDb.
    [ObservableProperty]
    private double noiseSnrDb;

    [ObservableProperty]
    private double noiseBandwidthHz;

    [ObservableProperty]
    private bool agcEnabled;

    [ObservableProperty]
    private double agcDelaySeconds;

    [ObservableProperty]
    private double agcMaxGainDb;

    [ObservableProperty]
    private bool apfEnabled;

    [ObservableProperty]
    private double apfBandwidthHz;

    [ObservableProperty]
    private double apfPeakGainDb;

    [ObservableProperty]
    private bool qsbEnabled;

    [ObservableProperty]
    private double qsbDepthDb;

    [ObservableProperty]
    private double qsbPeriodSeconds;

    [ObservableProperty]
    private double errorThreshold;

    /// <summary>
    /// When enabled, the practice WPM is adjusted in memory after each scored session based on
    /// the average error rate of the last <see cref="AutoAdjustWindowSize"/> sessions, with a
    /// failing newest session always forcing a slow-down.
    /// The dynamic WPM is never persisted; only this toggle and the window size are saved.
    /// </summary>
    [ObservableProperty]
    private bool autoAdjustWpm;

    [ObservableProperty]
    private int autoAdjustWindowSize;

    /// <summary>
    /// User-supplied text to send instead of generated groups; blank means "generate as usual".
    /// </summary>
    [ObservableProperty]
    private string customText = string.Empty;

    [ObservableProperty]
    private string characterSetsText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>
    /// Empty unless the current audio and noise settings drive the output past full scale.
    /// Shown next to the dialog buttons: the settings are still saveable, they just distort.
    /// </summary>
    [ObservableProperty]
    private string distortionWarning = string.Empty;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public event Action<bool>? CloseRequested;

    public MorseSettingsDialogViewModel(AppConfig config, IPracticeSettingsValidator settingsValidator, IAudioHeadroomAnalyzer headroomAnalyzer, ICharacterSetTextCodec characterSetTextCodec)
    {
        _settingsValidator = settingsValidator;
        _headroomAnalyzer = headroomAnalyzer;
        _characterSetTextCodec = characterSetTextCodec;
        _defaultDurationMins = config.Practice.DefaultDurationMins;
        _defaultCharacterSet = config.Practice.DefaultCharacterSet;

        CharacterWpm = config.Practice.CharacterWpm;
        AverageWpm = config.Practice.AverageWpm;
        WpmLocked = config.Practice.CharacterWpm == config.Practice.AverageWpm;
        SelectedSampleRate = config.Audio.SampleRate;
        Frequency = config.Audio.Frequency;
        VolumeDb = config.Audio.VolumeDb;
        BeepRampMs = config.Audio.BeepRampMs;
        SelectedNoiseType = config.Audio.Noise.Type;
        NoiseSnrDb = -config.Audio.Noise.LevelDb;
        NoiseBandwidthHz = config.Audio.Noise.BandwidthHz;
        AgcEnabled = config.Audio.Noise.AgcEnabled;
        AgcDelaySeconds = config.Audio.Noise.AgcDelaySeconds;
        AgcMaxGainDb = config.Audio.Noise.AgcMaxGainDb;
        ApfEnabled = config.Audio.Noise.ApfEnabled;
        ApfBandwidthHz = config.Audio.Noise.ApfBandwidthHz;
        ApfPeakGainDb = config.Audio.Noise.ApfPeakGainDb;
        QsbEnabled = config.Audio.Noise.QsbEnabled;
        QsbDepthDb = config.Audio.Noise.QsbDepthDb;
        QsbPeriodSeconds = config.Audio.Noise.QsbPeriodSeconds;
        ErrorThreshold = config.Practice.ErrorThreshold;
        AutoAdjustWpm = config.Practice.AutoAdjustWpm;
        AutoAdjustWindowSize = config.Practice.AutoAdjustWindowSize;
        CustomText = config.Practice.CustomText ?? string.Empty;

        if (!SampleRateOptions.Contains(SelectedSampleRate))
        {
            SelectedSampleRate = 44100;
        }

        CharacterSetsText = _characterSetTextCodec.FormatForEditor(config.CharacterSets);

        SaveCommand = new RelayCommand(OnSave);
        CancelCommand = new RelayCommand(OnCancel);

        // One handler instead of eighteen OnXChanged hooks; the recompute is a short render,
        // so it can run inline as the user turns a knob.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && AudioChainPropertyNames.Contains(name))
                UpdateDistortionWarning();
        };

        UpdateDistortionWarning();
    }

    /// <summary>
    /// Renders a probe message through the real signal chain and reports how far past full
    /// scale it goes. Everything above full scale is hard-clipped in the player before the
    /// buffer reaches the operating system, so the audible result is distortion.
    /// </summary>
    private void UpdateDistortionWarning()
    {
        var report = _headroomAnalyzer.Analyze(MorsePlaybackSettings.From(BuildAudio(), CharacterWpm, AverageWpm));

        DistortionWarning = report.WillClip
            ? $"⚠ Distortion: peaks {report.PeakDbFs:+0.#;-0.#} dB over FS, {FormatPercent(report.ClippedSampleRatio)} of samples clipped."
            : string.Empty;
    }

    private static string FormatPercent(double ratio)
    {
        double percent = ratio * 100.0;
        return percent < 0.1 ? "<0.1 %" : $"{percent:0.#} %";
    }

    public bool TryBuildSettings(out AppConfig settings)
    {
        settings = new AppConfig();

        if (!_characterSetTextCodec.TryParse(CharacterSetsText, out var parsedSets, out var parserError))
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
                CustomText = (CustomText ?? string.Empty).Trim(),
                AutoAdjustWpm = AutoAdjustWpm,
                AutoAdjustWindowSize = AutoAdjustWindowSize,
            },
            Audio = BuildAudio(),
            CharacterSets = characterSets,
        };
    }

    private Audio BuildAudio() => new()
    {
        SampleRate = SelectedSampleRate,
        Frequency = Frequency,
        VolumeDb = VolumeDb,
        BeepRampMs = BeepRampMs,
        Noise = new NoiseSettings
        {
            Type = SelectedNoiseType,
            LevelDb = -NoiseSnrDb,
            BandwidthHz = NoiseBandwidthHz,
            AgcEnabled = AgcEnabled,
            AgcDelaySeconds = AgcDelaySeconds,
            AgcMaxGainDb = AgcMaxGainDb,
            ApfEnabled = ApfEnabled,
            ApfBandwidthHz = ApfBandwidthHz,
            ApfPeakGainDb = ApfPeakGainDb,
            QsbEnabled = QsbEnabled,
            QsbDepthDb = QsbDepthDb,
            QsbPeriodSeconds = QsbPeriodSeconds,
        },
    };
}
