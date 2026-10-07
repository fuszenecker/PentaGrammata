using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Presentation;

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
    private readonly IUiDispatcher? _uiDispatcher;
    private readonly SynchronizationContext? _synchronizationContext;
    private CancellationTokenSource? _headroomAnalysisCancellation;
    private int _headroomRequestId;
    private bool _dialogClosed;
    private static readonly TimeSpan HeadroomDebounce = TimeSpan.FromMilliseconds(150);

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

    public MorseSettingsDialogViewModel(
        AppConfig config,
        IPracticeSettingsValidator settingsValidator,
        IAudioHeadroomAnalyzer headroomAnalyzer,
        ICharacterSetTextCodec characterSetTextCodec,
        IUiDispatcher? uiDispatcher = null)
    {
        _settingsValidator = settingsValidator;
        _headroomAnalyzer = headroomAnalyzer;
        _characterSetTextCodec = characterSetTextCodec;
        _uiDispatcher = uiDispatcher;
        _synchronizationContext = SynchronizationContext.Current;
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

        // Coalesce coupled property changes and supersede any older probe.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && AudioChainPropertyNames.Contains(name))
                ScheduleDistortionWarningUpdate();
        };

        ScheduleDistortionWarningUpdate();
    }

    /// <summary>
    /// Cancels pending headroom work when the settings dialog closes.
    /// </summary>
    public void OnDialogClosed()
    {
        if (_dialogClosed)
        {
            return;
        }

        _dialogClosed = true;
        _headroomRequestId++;
        var cancellation = _headroomAnalysisCancellation;
        _headroomAnalysisCancellation = null;
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A dispatcher shutdown may have ended the completion callback first.
        }
    }

    private void ScheduleDistortionWarningUpdate()
    {
        if (_dialogClosed)
        {
            return;
        }

        var settings = MorsePlaybackSettings.From(BuildAudio(), CharacterWpm, AverageWpm);
        var cancellation = new CancellationTokenSource();
        var previous = _headroomAnalysisCancellation;
        var requestId = ++_headroomRequestId;
        _headroomAnalysisCancellation = cancellation;
        previous?.Cancel();

        DistortionWarning = "Checking audio headroom…";
        _ = AnalyzeHeadroomAsync(settings, cancellation, requestId);
    }

    private async Task AnalyzeHeadroomAsync(
        MorsePlaybackSettings settings,
        CancellationTokenSource cancellation,
        int requestId)
    {
        var cancellationToken = cancellation.Token;
        AudioHeadroomReport? report = null;
        var failed = false;

        try
        {
            await Task.Delay(HeadroomDebounce, cancellationToken).ConfigureAwait(false);
            report = await Task.Run(
                () => _headroomAnalyzer.Analyze(settings, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer edit or canceled when the dialog closed.
        }
        catch (Exception)
        {
            failed = true;
        }

        try
        {
            await InvokeOnUiAsync(() =>
            {
                if (!_dialogClosed && requestId == _headroomRequestId && !cancellationToken.IsCancellationRequested)
                {
                    DistortionWarning = failed
                        ? "Headroom check failed."
                        : FormatDistortionWarning(report!.Value);
                }

                if (ReferenceEquals(_headroomAnalysisCancellation, cancellation))
                {
                    _headroomAnalysisCancellation = null;
                }

                cancellation.Dispose();
            }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The UI dispatcher can stop accepting work during application shutdown. The
            // background task is still observed, and its cancellation resources are released.
            cancellation.Dispose();
        }
    }

    private Task InvokeOnUiAsync(Action action)
    {
        if (_uiDispatcher is not null)
        {
            return _uiDispatcher.InvokeAsync(action);
        }

        if (_synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _synchronizationContext.Post(_ =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);
        return completion.Task;
    }

    private static string FormatDistortionWarning(AudioHeadroomReport report)
    {
        // Everything above full scale is hard-clipped by the player before reaching the
        // sound device, so the warning includes its peak and clipped-sample share.
        return report.WillClip
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
