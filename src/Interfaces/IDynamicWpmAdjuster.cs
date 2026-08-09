namespace PentaGrammata.Interfaces;

/// <summary>
/// Holds the in-memory dynamic WPM state and the auto-adjust math used by practice.
/// The state is never persisted: it restarts from the configured WPM on every app start
/// and whenever settings are applied. Only the auto-adjust toggle and window size are
/// saved with the configuration.
/// </summary>
public interface IDynamicWpmAdjuster
{
    /// <summary>Current in-memory character WPM.</summary>
    int DynamicCharacterWpm { get; }

    /// <summary>Current in-memory average (Farnsworth) WPM.</summary>
    int DynamicAverageWpm { get; }

    /// <summary>
    /// Restarts the in-memory progression from the given configured WPM values and clears
    /// the recent error-rate history. Called on construction and whenever settings change.
    /// </summary>
    void Reset(int characterWpm, int averageWpm);

    /// <summary>
    /// Records a session error rate and nudges the dynamic WPM by the average of the last
    /// <paramref name="windowSize"/> sessions (see <see cref="DynamicWpmAdjuster.Adjust"/>).
    /// </summary>
    void Adjust(double errorRatePercent, double errorThreshold, int windowSize);
}
