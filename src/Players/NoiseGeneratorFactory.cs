using System;
using PentaGrammata.Configuration;

namespace PentaGrammata.Players;

/// <summary>
/// Creates the configured noise generator. Playback draws from <see cref="Random.Shared"/>;
/// pass an explicit seeded <see cref="Random"/> when a render has to be reproducible (the
/// headroom analyzer does, so its verdict does not wobble between identical settings).
/// </summary>
public sealed class NoiseGeneratorFactory(Random? random = null) : INoiseGeneratorFactory
{
    private readonly Random _random = random ?? Random.Shared;

    public INoiseGenerator? Create(NoiseType type) => type switch
    {
        NoiseType.Gaussian => new GaussianNoiseGenerator(_random),
        NoiseType.Uniform => new UniformNoiseGenerator(_random),
        NoiseType.Pink => new PinkNoiseGenerator(_random),
        _ => null,
    };
}
