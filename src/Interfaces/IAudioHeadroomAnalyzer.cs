using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Predicts whether a set of audio settings will drive the output past 16-bit full scale,
/// which the player has to clip and which is therefore audible as distortion.
/// </summary>
public interface IAudioHeadroomAnalyzer
{
    AudioHeadroomReport Analyze(MorsePlaybackSettings settings);
}
