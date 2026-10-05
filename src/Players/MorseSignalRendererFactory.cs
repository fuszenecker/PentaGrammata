using System;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

public sealed class MorseSignalRendererFactory(INoiseGeneratorFactory noiseGeneratorFactory) : IMorseSignalRendererFactory
{
    private readonly INoiseGeneratorFactory _noiseGeneratorFactory = noiseGeneratorFactory;

    public IMorseSignalRenderer Create(Random random) => new MorseSignalRenderer(_noiseGeneratorFactory, random);
}
