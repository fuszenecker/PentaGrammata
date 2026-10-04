using PentaGrammata.Configuration;

namespace PentaGrammata.Models;

/// <summary>
/// Immutable bundle of the parameters needed to render Morse code to audio.
/// Replaces a long positional parameter list on <see cref="PentaGrammata.Interfaces.IMorsePlayer"/>.
/// </summary>
public sealed record MorsePlaybackSettings
{
    public required int CharacterWpm { get; init; }
    public required int AverageWpm { get; init; }
    public required int SampleRate { get; init; }
    public required double Frequency { get; init; }

    /// <summary>CW signal level in dBFS (0 dB = full scale, negative = quieter).</summary>
    public required double VolumeDb { get; init; }
    public required int BeepRampMs { get; init; }

    /// <summary>Background-noise type; defaults to <see cref="NoiseType.None"/> (clean signal).</summary>
    public NoiseType NoiseType { get; init; } = NoiseType.None;

    /// <summary>
    /// Noise level relative to the tone (<see cref="VolumeDb"/>), in decibels, measured
    /// after the shared passband — i.e. the in-band signal-to-noise ratio actually heard,
    /// independent of <see cref="NoiseBandwidthHz"/>.
    /// </summary>
    public double NoiseLevelDb { get; init; } = -15.0;

    /// <summary>Width of the shared receiver filter centered on <see cref="Frequency"/>, in Hz.</summary>
    public double NoiseBandwidthHz { get; init; } = 500.0;

    /// <summary>Whether the AGC stage is active.</summary>
    public bool AgcEnabled { get; init; } = true;

    /// <summary>AGC release/delay, in seconds.</summary>
    public double AgcDelaySeconds { get; init; } = 0.4;

    /// <summary>Maximum AGC boost of a weak signal / noise floor, in decibels.</summary>
    public double AgcMaxGainDb { get; init; } = 18.0;

    /// <summary>Whether the audio peak filter (APF) stage is active.</summary>
    public bool ApfEnabled { get; init; } = true;

    /// <summary>Width of the APF's resonant peak at the tone, in Hz.</summary>
    public double ApfBandwidthHz { get; init; } = 120.0;

    /// <summary>Peak amplification of the peak-filtered signal blended in, in decibels.</summary>
    public double ApfPeakGainDb { get; init; } = -9.0;

    /// <summary>Whether QSB (signal fading) is applied to the generated Morse audio.</summary>
    public bool QsbEnabled { get; init; }

    /// <summary>Deepest fade below full signal strength, in decibels.</summary>
    public double QsbDepthDb { get; init; } = 10.0;

    /// <summary>Fade period, in seconds; larger = slower fading.</summary>
    public double QsbPeriodSeconds { get; init; } = 5.0;

    /// <summary>
    /// Maps the audio configuration plus the speeds actually in use onto playback settings, so
    /// playback and the headroom analysis cannot drift apart on how a setting is carried over.
    /// </summary>
    public static MorsePlaybackSettings From(Audio audio, int characterWpm, int averageWpm) => new()
    {
        CharacterWpm = characterWpm,
        AverageWpm = averageWpm,
        SampleRate = audio.SampleRate,
        Frequency = audio.Frequency,
        VolumeDb = audio.VolumeDb,
        BeepRampMs = audio.BeepRampMs,
        NoiseType = audio.Noise.Type,
        NoiseLevelDb = audio.Noise.LevelDb,
        NoiseBandwidthHz = audio.Noise.BandwidthHz,
        AgcEnabled = audio.Noise.AgcEnabled,
        AgcDelaySeconds = audio.Noise.AgcDelaySeconds,
        AgcMaxGainDb = audio.Noise.AgcMaxGainDb,
        ApfEnabled = audio.Noise.ApfEnabled,
        ApfBandwidthHz = audio.Noise.ApfBandwidthHz,
        ApfPeakGainDb = audio.Noise.ApfPeakGainDb,
        QsbEnabled = audio.Noise.QsbEnabled,
        QsbDepthDb = audio.Noise.QsbDepthDb,
        QsbPeriodSeconds = audio.Noise.QsbPeriodSeconds,
    };
}
