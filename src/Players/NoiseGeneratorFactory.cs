using System;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

/// <summary>
/// Creates the configured noise generator. Playback draws from <see cref="Random.Shared"/>;
/// pass an explicit seeded <see cref="Random"/> when a render has to be reproducible (the
/// headroom analyzer does, so its verdict does not wobble between identical settings).
/// </summary>
public sealed class NoiseGeneratorFactory(Random? random = null) : INoiseGeneratorFactory
{
    private readonly Random _random = random ?? Random.Shared;

    public INoiseGenerator? Create(NoiseType type) => Create(type, _random);

    public INoiseGenerator? Create(NoiseType type, Random random) => type switch
    {
        NoiseType.Gaussian => new GaussianNoiseGenerator(random),
        NoiseType.Uniform => new UniformNoiseGenerator(random),
        NoiseType.Pink => new PinkNoiseGenerator(random),
        _ => null,
    };
}
