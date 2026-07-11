using PentaGrammata.Configuration;

namespace PentaGrammata.Services;

public interface INoiseGeneratorFactory
{
    /// <summary>
    /// Creates a generator for the requested noise type, or <c>null</c> for
    /// <see cref="NoiseType.None"/> (meaning: mix in no noise at all).
    /// </summary>
    INoiseGenerator? Create(NoiseType type);
}
