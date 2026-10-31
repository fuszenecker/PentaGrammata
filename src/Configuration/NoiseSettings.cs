using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// Background-noise parameters mixed under the Morse tone. By default there is no
/// noise (<see cref="Type"/> == <see cref="NoiseType.None"/>).
/// </summary>
[PromptuariumComment("The simulated receiver: background noise, filters, AGC and fading (QSB)")]
public sealed class NoiseSettings
{
    /// <summary>Which noise generator to use, or <see cref="NoiseType.None"/> for a clean signal.</summary>
    [PromptuariumComment("The noise generator: None, Gaussian, Uniform or Pink")]
    public NoiseType Type { get; set; } = NoiseType.None;

    /// <summary>
    /// Noise level relative to the Morse tone, in decibels. Negative values place the
    /// noise below the tone. The reference tone level already accounts for the audio
    /// volume (loudness) setting. This is the in-band level heard after the receiver
    /// filter, so it is a true signal-to-noise ratio and does not drift as
    /// <see cref="BandwidthHz"/> changes.
    /// </summary>
    [PromptuariumComment("The in-band noise level relative to the tone, in dB (negative is below the tone): the signal-to-noise ratio")]
    public double LevelDb { get; set; } = -15.0;

    /// <summary>Width, in Hz, of the shared receiver filter, centered on the tone frequency.</summary>
    [PromptuariumComment("The width of the receiver filter centered on the tone, in Hz")]
    public double BandwidthHz { get; set; } = 500.0;

    /// <summary>
    /// When true, an automatic gain control rides the combined signal so the noise floor
    /// breathes up in the gaps and ducks under the tone. When false the level is left flat.
    /// </summary>
    [PromptuariumComment("Whether an automatic gain control rides the signal (the noise rises in the gaps)")]
    public bool AgcEnabled { get; set; } = true;

    /// <summary>
    /// AGC release ("delay"), in seconds: how slowly the gain recovers after a signal
    /// ends. Larger values keep the noise floor suppressed longer between characters.
    /// </summary>
    [PromptuariumComment("The AGC release time, in seconds: how slowly the gain recovers after the signal ends")]
    public double AgcDelaySeconds { get; set; } = 0.4;

    /// <summary>
    /// Maximum amount the AGC may boost a weak signal or the noise floor, in decibels.
    /// Caps the gain so the gaps between characters don't swell uncontrollably.
    /// 18 dB ≈ 8× (default). Lower = quieter gaps (easier copy); higher = louder noise
    /// floor (more realistic weak-signal feel, harder practice).
    /// </summary>
    [PromptuariumComment("The largest boost the AGC may apply, in dB")]
    public double AgcMaxGainDb { get; set; } = 18.0;

    /// <summary>
    /// When true, an audio peak filter (APF) adds a resonant peak at the tone for the
    /// characteristic CW "ring". When false the tone passes through the wider filter only.
    /// </summary>
    [PromptuariumComment("Whether an audio peak filter adds a resonant peak at the tone (the CW ring)")]
    public bool ApfEnabled { get; set; } = true;

    /// <summary>Width, in Hz, of the audio peak filter's resonant peak at the tone.</summary>
    [PromptuariumComment("The width of the audio peak filter, in Hz")]
    public double ApfBandwidthHz { get; set; } = 120.0;

    /// <summary>
    /// Blend gain of the narrow-peak-filtered signal added on top of the passband, in
    /// decibels relative to the passband signal level after AGC. The APF output is scaled
    /// by <c>10^(ApfPeakGainDb/20)</c> and added to the passband: 0 dB adds the peak at
    /// the same amplitude as the passband (very prominent ring); −6 dB at half amplitude;
    /// −9 dB (default) at ~35 % for a subtle CW ring. Positive values make the peak
    /// dominate above the passband.
    /// </summary>
    [PromptuariumComment("The level of the peak-filtered signal added to the passband, in dB (0 is equal, negative is subtler)")]
    public double ApfPeakGainDb { get; set; } = -9.0;

    /// <summary>
    /// When true, the generated Morse signal is faded by simulated ionospheric QSB before
    /// the receiver chain: its strength wanders down and recovers unpredictably,
    /// independently of whether noise is enabled. When false the signal strength is constant.
    /// </summary>
    [PromptuariumComment("Whether the signal fades in and out like ionospheric QSB")]
    public bool QsbEnabled { get; set; }

    /// <summary>
    /// Deepest fade below full signal strength, in decibels. The signal wanders between
    /// full strength and this depth. 10 dB ≈ 3× down (default).
    /// </summary>
    [PromptuariumComment("The deepest fade below full signal strength, in dB")]
    public double QsbDepthDb { get; set; } = 10.0;

    /// <summary>
    /// Fade period, in seconds: roughly how long one fade cycle takes. Larger = slower,
    /// more gradual fading; smaller = faster, choppier fading. 5 s (default).
    /// </summary>
    [PromptuariumComment("The approximate length of one fade cycle, in seconds")]
    public double QsbPeriodSeconds { get; set; } = 5.0;

    public NoiseSettings Clone() => new()
    {
        Type = Type,
        LevelDb = LevelDb,
        BandwidthHz = BandwidthHz,
        AgcEnabled = AgcEnabled,
        AgcDelaySeconds = AgcDelaySeconds,
        AgcMaxGainDb = AgcMaxGainDb,
        ApfEnabled = ApfEnabled,
        ApfBandwidthHz = ApfBandwidthHz,
        ApfPeakGainDb = ApfPeakGainDb,
        QsbEnabled = QsbEnabled,
        QsbDepthDb = QsbDepthDb,
        QsbPeriodSeconds = QsbPeriodSeconds,
    };
}
