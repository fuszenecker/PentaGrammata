using System.Threading;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Predicts whether a set of audio settings will drive the output past 16-bit full scale,
/// which the player has to clip and which is therefore audible as distortion.
/// </summary>
public interface IAudioHeadroomAnalyzer
{
    /// <summary>Analyzes settings, checking cancellation periodically during DSP rendering.</summary>
    AudioHeadroomReport Analyze(MorsePlaybackSettings settings, CancellationToken cancellationToken = default);
}
