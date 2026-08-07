using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PentaGrammata.Interfaces;

namespace PentaGrammata.Services;

/// <summary>
/// Holds the in-memory dynamic WPM state and the auto-adjust math. Never persisted: the
/// dynamic WPM restarts from the configured WPM on every app start (and whenever settings are
/// applied). Only the AutoAdjustWpm toggle and window size are saved with the configuration.
/// </summary>
public class DynamicWpmAdjuster : IDynamicWpmAdjuster
{
    private const int AutoAdjustStep = 1;
    private const int MinWpm = 1;

    private readonly Queue<double> _recentErrorRates = new();
    private readonly ILogger<DynamicWpmAdjuster> _logger;

    private int _dynamicCharacterWpm;
    private int _dynamicAverageWpm;

    public DynamicWpmAdjuster(ILogger<DynamicWpmAdjuster> logger)
    {
        _logger = logger;
    }

    public int DynamicCharacterWpm => _dynamicCharacterWpm;

    public int DynamicAverageWpm => _dynamicAverageWpm;

    public void Reset(int characterWpm, int averageWpm)
    {
        _dynamicCharacterWpm = characterWpm;
        _dynamicAverageWpm = averageWpm;
        _recentErrorRates.Clear();
    }

    /// <summary>
    /// Records the session error rate and nudges the dynamic WPM by the average error rate of
    /// the last N sessions: above the threshold slows the average WPM down, at or below the
    /// threshold speeds it up. The session that just finished also has a veto — if it alone is
    /// above the threshold the speed drops even when the window average still looks good, so a
    /// fresh failure is never averaged away by earlier clean sessions. When speeding up and the
    /// average WPM reaches the character WPM, the character WPM is raised too so the average
    /// can keep climbing (Farnsworth spacing collapses to zero, then raw speed increases). The
    /// window fills up from the start of the application, so early on the average is taken over
    /// however many sessions exist so far.
    /// </summary>
    public void Adjust(double errorRatePercent, double errorThreshold, int windowSize)
    {
        _recentErrorRates.Enqueue(errorRatePercent);
        while (_recentErrorRates.Count > windowSize)
        {
            _recentErrorRates.Dequeue();
        }

        var averageErrorRate = _recentErrorRates.Average();

        if (averageErrorRate > errorThreshold || errorRatePercent > errorThreshold)
        {
            // Struggling, or the newest session failed on its own: slow down. Lowering the
            // average WPM adds Farnsworth spacing; the character WPM is left untouched unless
            // it would fall below the new average.
            _dynamicAverageWpm = Math.Max(MinWpm, _dynamicAverageWpm - AutoAdjustStep);
            if (_dynamicCharacterWpm < _dynamicAverageWpm)
            {
                _dynamicCharacterWpm = _dynamicAverageWpm;
            }
        }
        else
        {
            // Newest session passed and the window average is good too: speed up. If the
            // average would overtake the character WPM, raise the character WPM so the two
            // stay valid (average <= character).
            var newAverage = _dynamicAverageWpm + AutoAdjustStep;
            if (newAverage > _dynamicCharacterWpm)
            {
                _dynamicCharacterWpm = newAverage;
            }
            _dynamicAverageWpm = newAverage;
        }

        _logger.LogInformation(
            "Dynamic WPM adjusted to character {CharWpm} / average {AvgWpm} (last error {LastError:F2}%, avg error {AvgError:F2}% over {Count} sessions)",
            _dynamicCharacterWpm, _dynamicAverageWpm, errorRatePercent, averageErrorRate, _recentErrorRates.Count);
    }
}
