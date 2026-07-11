using System;
using PentaGrammata.Configuration;

namespace PentaGrammata.Services;

public sealed class NoiseGeneratorFactory : INoiseGeneratorFactory
{
    public INoiseGenerator? Create(NoiseType type) => type switch
    {
        NoiseType.Gaussian => new GaussianNoiseGenerator(Random.Shared),
        NoiseType.Uniform => new UniformNoiseGenerator(Random.Shared),
        NoiseType.Pink => new PinkNoiseGenerator(Random.Shared),
        _ => null,
    };
}
