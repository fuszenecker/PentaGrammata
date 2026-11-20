namespace PentaGrammata.Models;

/// <summary>
/// What one auto-adjust decision moves: the speed, the signal-to-noise ratio and the QSB depth,
/// each only when its own toggle is on (and, for the levels, when noise or QSB is active at all),
/// with the step of each level in decibels.
/// </summary>
public sealed record AutoAdjustTargets
{
    public static readonly AutoAdjustTargets WpmOnly = new() { Wpm = true };

    public bool Wpm { get; init; }

    public bool Snr { get; init; }

    /// <summary>How many dB the signal-to-noise ratio drops after a pass and rises after a failure.</summary>
    public double SnrStepDb { get; init; } = 1.0;

    public bool Qsb { get; init; }

    /// <summary>How many dB the QSB fade deepens after a pass and becomes shallower after a failure.</summary>
    public double QsbStepDb { get; init; } = 1.0;

    public bool Any => Wpm || Snr || Qsb;
}
