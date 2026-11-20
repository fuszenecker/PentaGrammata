using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Holds the in-memory dynamic difficulty and the auto-adjust math. The speed, the signal-to-noise
/// ratio and the QSB depth restart from the configured values on every app start (and whenever
/// settings are applied); the controller saves the adjusted levels into the settings, the WPM not.
/// </summary>
public class DynamicDifficultyAdjuster : IDynamicDifficultyAdjuster
{
    private const int WpmStep = 1;
    private const int MinWpm = 1;

    // The ranges the settings dialog offers for the two levels; the dynamic values stay inside
    // them so they never drift to something the user could not have configured.
    public const double MinSnrDb = -20;
    public const double MaxSnrDb = 80;
    public const double MinQsbDepthDb = 1;
    public const double MaxQsbDepthDb = 40;

    private readonly Queue<double> _recentErrorRates = new();
    private readonly ILogger<DynamicDifficultyAdjuster> _logger;

    private int _dynamicCharacterWpm;
    private int _dynamicAverageWpm;
    private double _dynamicSnrDb;
    private double _dynamicQsbDepthDb;

    public DynamicDifficultyAdjuster(ILogger<DynamicDifficultyAdjuster> logger)
    {
        _logger = logger;
    }

    public int DynamicCharacterWpm => _dynamicCharacterWpm;

    public int DynamicAverageWpm => _dynamicAverageWpm;

    public double DynamicSnrDb => _dynamicSnrDb;

    public double DynamicQsbDepthDb => _dynamicQsbDepthDb;

    public void Reset(int characterWpm, int averageWpm, double snrDb, double qsbDepthDb)
    {
        _dynamicCharacterWpm = characterWpm;
        _dynamicAverageWpm = averageWpm;
        _dynamicSnrDb = snrDb;
        _dynamicQsbDepthDb = qsbDepthDb;
        _recentErrorRates.Clear();
    }

    /// <summary>
    /// Records the session error rate and decides by the average error rate of the last N
    /// sessions: above the threshold makes practice easier, at or below it harder. The session
    /// that just finished also has a veto — if it alone is above the threshold practice gets
    /// easier even when the window average still looks good, so a fresh failure is never
    /// averaged away by earlier clean sessions. The one decision moves every enabled quantity:
    /// <list type="bullet">
    /// <item>the average WPM by 1 (when it reaches the character WPM while speeding up, the
    /// character WPM is raised too so the average can keep climbing);</item>
    /// <item>the signal-to-noise ratio by its step: lower is harder;</item>
    /// <item>the QSB depth by its step: deeper is harder.</item>
    /// </list>
    /// The window fills up from the start of the application, so early on the average is taken
    /// over however many sessions exist so far.
    /// </summary>
    public void Adjust(double errorRatePercent, double errorThreshold, int windowSize, AutoAdjustTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        _recentErrorRates.Enqueue(errorRatePercent);
        while (_recentErrorRates.Count > windowSize)
        {
            _recentErrorRates.Dequeue();
        }

        var averageErrorRate = _recentErrorRates.Average();
        var easier = averageErrorRate > errorThreshold || errorRatePercent > errorThreshold;

        if (targets.Wpm)
        {
            AdjustWpm(easier);
        }

        if (targets.Snr)
        {
            var step = easier ? targets.SnrStepDb : -targets.SnrStepDb;
            _dynamicSnrDb = Math.Clamp(_dynamicSnrDb + step, MinSnrDb, MaxSnrDb);
        }

        if (targets.Qsb)
        {
            var step = easier ? -targets.QsbStepDb : targets.QsbStepDb;
            _dynamicQsbDepthDb = Math.Clamp(_dynamicQsbDepthDb + step, MinQsbDepthDb, MaxQsbDepthDb);
        }

        _logger.LogInformation(
            "Practice made {Direction}: character {CharWpm} / average {AvgWpm} WPM, SNR {Snr:F1} dB, QSB {Qsb:F1} dB (last error {LastError:F2}%, avg error {AvgError:F2}% over {Count} sessions)",
            easier ? "easier" : "harder", _dynamicCharacterWpm, _dynamicAverageWpm, _dynamicSnrDb, _dynamicQsbDepthDb,
            errorRatePercent, averageErrorRate, _recentErrorRates.Count);
    }

    private void AdjustWpm(bool easier)
    {
        if (easier)
        {
            // Lowering the average WPM adds Farnsworth spacing; the character WPM is left
            // untouched unless it would fall below the new average.
            _dynamicAverageWpm = Math.Max(MinWpm, _dynamicAverageWpm - WpmStep);
            if (_dynamicCharacterWpm < _dynamicAverageWpm)
            {
                _dynamicCharacterWpm = _dynamicAverageWpm;
            }

            return;
        }

        // If the average would overtake the character WPM, raise the character WPM so the two
        // stay valid (average <= character).
        var newAverage = _dynamicAverageWpm + WpmStep;
        if (newAverage > _dynamicCharacterWpm)
        {
            _dynamicCharacterWpm = newAverage;
        }
        _dynamicAverageWpm = newAverage;
    }
}
