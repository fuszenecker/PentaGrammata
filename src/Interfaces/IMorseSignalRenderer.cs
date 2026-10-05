using System.Threading;

using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Renders a Morse message through the configured signal chain without clamping its output.
/// </summary>
public interface IMorseSignalRenderer
{
    /// <summary>Sample value that corresponds to 0 dBFS.</summary>
    const double FullScale = short.MaxValue;

    double[] Render(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken = default);
}
