namespace PentaGrammata.Configuration;

/// <summary>
/// Kind of background noise mixed under the Morse tone. <see cref="None"/> is the
/// default and produces a perfectly clean signal.
/// </summary>
public enum NoiseType
{
    None,
    Gaussian,
    Uniform,
    Pink,
}
