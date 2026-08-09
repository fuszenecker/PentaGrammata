namespace PentaGrammata.Players;

/// <summary>
/// Produces a stream of raw background-noise samples. The absolute amplitude is
/// unspecified: callers band-limit and then rescale the noise to the desired level,
/// so a generator only needs to define the noise's <em>spectral shape</em>.
/// </summary>
public interface INoiseGenerator
{
    /// <summary>Returns the next raw noise sample.</summary>
    double Next();
}
