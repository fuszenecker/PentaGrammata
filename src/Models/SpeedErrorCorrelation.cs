using System;
using System.Collections.Generic;

namespace PentaGrammata.Models;

/// <summary>
/// One saved session placed on the correlation plane: the average speed it ran at against the
/// error rate it produced.
/// </summary>
public sealed class SpeedErrorPoint
{
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Average (Farnsworth) speed of the session, in WPM: the horizontal coordinate.</summary>
    public double AverageWpm { get; init; }

    /// <summary>Error rate of the session, in percent: the vertical coordinate.</summary>
    public double ErrorRatePercent { get; init; }
}

/// <summary>
/// The scatter of average speed against error rate over the analysis window, plus the
/// least-squares line fitted through it. <see cref="HasFit"/> is false when the window holds
/// fewer than two sessions or when every session ran at the same speed (a vertical scatter has
/// no error-on-speed line); <see cref="Slope"/>, <see cref="Intercept"/> and
/// <see cref="PearsonR"/> are then zero and carry no meaning.
/// </summary>
public sealed class SpeedErrorCorrelation
{
    public IReadOnlyList<SpeedErrorPoint> Points { get; init; } = [];

    public bool HasFit { get; init; }

    /// <summary>Pearson correlation coefficient between speed and error rate, -1 to 1.</summary>
    public double PearsonR { get; init; }

    /// <summary>Slope of the fitted line: error-rate percentage points per WPM.</summary>
    public double Slope { get; init; }

    /// <summary>Error rate, in percent, the fitted line predicts at 0 WPM.</summary>
    public double Intercept { get; init; }

    /// <summary>
    /// Standard deviation of the residuals around the fitted line, in error-rate percentage
    /// points: the typical distance between a session and the line, so roughly two thirds of
    /// sessions fall within this much of it. Divides by n - 2 (the slope and the intercept each
    /// consume a degree of freedom), and is therefore zero for a two-session window, where the
    /// line passes exactly through both points and the spread is unmeasurable rather than
    /// genuinely absent.
    /// </summary>
    public double ResidualStandardDeviation { get; init; }
}
