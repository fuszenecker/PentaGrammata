using System;

namespace PentaGrammata.Players;

/// <summary>
/// A single-stage RBJ biquad band-pass filter (constant 0 dB peak gain). Used to limit
/// background noise to a band of a given width centered on the Morse tone frequency,
/// emulating a receiver's IF/audio filter. Instances are stateful: feed samples through
/// <see cref="Process"/> in order.
/// </summary>
public sealed class BandPassFilter
{
    private readonly double _b0;
    private readonly double _b1;
    private readonly double _b2;
    private readonly double _a1;
    private readonly double _a2;

    private double _x1;
    private double _x2;
    private double _y1;
    private double _y2;

    public BandPassFilter(double centerFrequencyHz, double bandwidthHz, int sampleRate)
    {
        // Clamp the center just inside Nyquist and keep the bandwidth positive so the
        // filter stays stable for any user-entered values.
        double nyquist = sampleRate / 2.0;
        double center = Math.Clamp(centerFrequencyHz, 1.0, nyquist - 1.0);
        double bandwidth = Math.Max(bandwidthHz, 1.0);

        double q = center / bandwidth;
        double omega = 2.0 * Math.PI * center / sampleRate;
        double sinOmega = Math.Sin(omega);
        double cosOmega = Math.Cos(omega);
        double alpha = sinOmega / (2.0 * q);

        // RBJ band-pass with constant 0 dB peak gain (normalized by a0).
        double a0 = 1.0 + alpha;
        _b0 = alpha / a0;
        _b1 = 0.0;
        _b2 = -alpha / a0;
        _a1 = (-2.0 * cosOmega) / a0;
        _a2 = (1.0 - alpha) / a0;
    }

    public double Process(double input)
    {
        double output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return output;
    }
}
