using System;

namespace PentaGrammata.Players;

/// <summary>
/// Gaussian (normal) white noise, generated with the Box-Muller transform. The raw
/// output is unbounded but has unit standard deviation.
/// </summary>
public sealed class GaussianNoiseGenerator(Random random) : INoiseGenerator
{
    private readonly Random _random = random;

    public double Next()
    {
        // Box-Muller: two uniforms in (0, 1] -> one standard-normal sample.
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

/// <summary>Uniform ("egyenletes") white noise spread evenly across [-1, 1].</summary>
public sealed class UniformNoiseGenerator(Random random) : INoiseGenerator
{
    private readonly Random _random = random;

    public double Next() => (_random.NextDouble() * 2.0) - 1.0;
}

/// <summary>
/// Pink (1/f) noise approximated with the Voss-McCartney / Paul Kellett filter over a
/// uniform white source. The output has roughly unit variance.
/// </summary>
public sealed class PinkNoiseGenerator(Random random) : INoiseGenerator
{
    private readonly Random _random = random;

    private double _b0;
    private double _b1;
    private double _b2;
    private double _b3;
    private double _b4;
    private double _b5;
    private double _b6;

    public double Next()
    {
        double white = (_random.NextDouble() * 2.0) - 1.0;

        _b0 = 0.99886 * _b0 + white * 0.0555179;
        _b1 = 0.99332 * _b1 + white * 0.0750759;
        _b2 = 0.96900 * _b2 + white * 0.1538520;
        _b3 = 0.86650 * _b3 + white * 0.3104856;
        _b4 = 0.55000 * _b4 + white * 0.5329522;
        _b5 = -0.7616 * _b5 - white * 0.0168980;

        double pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362;
        _b6 = white * 0.115926;

        // The Kellett coefficients sum to roughly 3.5x unit white; rescale back toward
        // unit variance so downstream dB scaling behaves like the white generators.
        return pink * 0.11;
    }
}
