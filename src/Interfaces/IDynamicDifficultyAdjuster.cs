using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Holds the in-memory dynamic difficulty (speed, signal-to-noise ratio, QSB depth) and the
/// auto-adjust math used by practice. The state is never persisted: it restarts from the
/// configured values on every app start and whenever settings are applied. Only the toggles,
/// the steps and the window size are saved with the configuration.
/// </summary>
public interface IDynamicDifficultyAdjuster
{
    /// <summary>Current in-memory character WPM.</summary>
    int DynamicCharacterWpm { get; }

    /// <summary>Current in-memory average (Farnsworth) WPM.</summary>
    int DynamicAverageWpm { get; }

    /// <summary>Current in-memory signal-to-noise ratio, in dB.</summary>
    double DynamicSnrDb { get; }

    /// <summary>Current in-memory deepest QSB fade, in dB.</summary>
    double DynamicQsbDepthDb { get; }

    /// <summary>
    /// Restarts the in-memory progression from the given configured values and clears the
    /// recent error-rate history. Called on construction and whenever settings change.
    /// </summary>
    void Reset(int characterWpm, int averageWpm, double snrDb, double qsbDepthDb);

    /// <summary>
    /// Records a session error rate, decides from the average of the last
    /// <paramref name="windowSize"/> sessions (and the newest session's veto) whether to make
    /// practice harder or easier, and moves every quantity <paramref name="targets"/> enables
    /// (see <see cref="PentaGrammata.Services.DynamicDifficultyAdjuster.Adjust"/>).
    /// </summary>
    void Adjust(double errorRatePercent, double errorThreshold, int windowSize, AutoAdjustTargets targets);
}
