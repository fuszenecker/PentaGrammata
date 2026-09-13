using System;

namespace PentaGrammata.Players;

/// <summary>
/// Simulates QSB — ionospheric signal fading. Instead of a regular sinusoid, the gain
/// wanders unpredictably: every retarget interval a new target is drawn uniformly in
/// decibels between full strength and the configured depth, and the gain eases toward it
/// with one-pole smoothing, so level changes are click-free and a fade transition is
/// roughly complete within one period. The gain never exceeds 1, so the faded signal can
/// never clip and silence stays silent. Instances are stateful: feed one buffer through
/// <see cref="Apply"/> in order, starting at full strength.
/// </summary>
public sealed class QsbFader
{
    private readonly double _depthDb;
    private readonly double _smoothing;
    private readonly int _retargetSamples;
    private readonly Random _random;

    private double _gain = 1.0;
    private double _targetGain = 1.0;
    private int _samplesUntilRetarget;

    public QsbFader(double depthDb, double periodSeconds, int sampleRate, Random random)
    {
        // Clamp guard values (BandPassFilter precedent) so hand-edited configs cannot
        // destabilize the fade: zero depth is an exact no-op, absurd periods become fast
        // but bounded flutter, and the retarget counter can never hit 0 or overflow.
        _depthDb = Math.Max(depthDb, 0.0);
        _random = random;

        // Retarget about twice per fade period; smooth with tau = period/3 so a
        // transition is ~95% complete within one period.
        _retargetSamples = (int)Math.Clamp(sampleRate * periodSeconds / 2.0, 1.0, int.MaxValue);
        double tauSamples = Math.Max(sampleRate * periodSeconds / 3.0, 1.0);
        _smoothing = 1.0 - Math.Exp(-1.0 / tauSamples);
        _samplesUntilRetarget = _retargetSamples;
    }

    public void Apply(short[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            if (--_samplesUntilRetarget <= 0)
            {
                // New fade depth: uniform in dB between 0 and the full depth, so the
                // signal occasionally dips the whole way down.
                _targetGain = DecibelsToLinear(-_random.NextDouble() * _depthDb);
                _samplesUntilRetarget = _retargetSamples;
            }

            // One-pole ease toward the target: |gain step| <= smoothing * (1 - minGain),
            // far below audibility, so retarget boundaries introduce no clicks either.
            _gain += _smoothing * (_targetGain - _gain);
            samples[i] = (short)(samples[i] * _gain);
        }
    }

    private static double DecibelsToLinear(double decibels) => Math.Pow(10.0, decibels / 20.0);
}