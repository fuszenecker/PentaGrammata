using System;

namespace PentaGrammata.Players;

/// <summary>
/// A simple envelope-following automatic gain control, like the AGC in a receiver.
/// A fast attack pulls the gain down as soon as a signal appears; a slow release lets
/// the gain climb back afterwards, so the noise floor audibly "breathes" up in the gaps
/// between characters and is pushed down under the signal. Instances are stateful: feed
/// samples through <see cref="Process"/> in order.
/// </summary>
public sealed class AutomaticGainControl
{
    private readonly double _attackCoefficient;
    private readonly double _releaseCoefficient;
    private readonly double _targetLevel;
    private readonly double _maxGain;

    private double _envelope;

    public AutomaticGainControl(
        int sampleRate,
        double targetLevel,
        double maxGain,
        double attackSeconds = 0.005,
        double releaseSeconds = 0.4)
    {
        // One-pole smoothing coefficients derived from the attack/release time constants.
        _attackCoefficient = 1.0 - Math.Exp(-1.0 / (attackSeconds * sampleRate));
        _releaseCoefficient = 1.0 - Math.Exp(-1.0 / (releaseSeconds * sampleRate));
        _targetLevel = targetLevel;
        _maxGain = maxGain;
    }

    public double Process(double input)
    {
        double rectified = Math.Abs(input);

        // Track the signal envelope: jump up fast, decay down slowly.
        double coefficient = rectified > _envelope ? _attackCoefficient : _releaseCoefficient;
        _envelope += coefficient * (rectified - _envelope);

        // Hold the output near the target level, but never boost quieter passages by
        // more than _maxGain (that ceiling is what keeps noise below the signal).
        double gain = _envelope > 1.0 ? Math.Min(_targetLevel / _envelope, _maxGain) : _maxGain;
        return input * gain;
    }
}
