namespace PentaGrammata.Models;

/// <summary>
/// How close a set of audio settings runs to 16-bit full scale, measured on a probe message
/// pushed through the real receiver chain.
/// </summary>
/// <param name="PeakDbFs">
/// Loudest sample of the rendered probe, in dB relative to full scale, BEFORE clipping.
/// 0 dBFS is exactly full scale; anything above it gets flattened on playback.
/// </param>
/// <param name="ClippedSampleRatio">Fraction of samples (0..1) that exceed full scale.</param>
/// <param name="ProbeSeconds">Length of audio the verdict was measured over.</param>
public readonly record struct AudioHeadroomReport(double PeakDbFs, double ClippedSampleRatio, double ProbeSeconds)
{
    /// <summary>True when the chain overshoots full scale, so playback will be hard-clipped.</summary>
    public bool WillClip => PeakDbFs > 0.0;
}
