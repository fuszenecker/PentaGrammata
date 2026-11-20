using System;
using System.Collections.Generic;

namespace PentaGrammata.Models;

/// <summary>
/// The quantity a correlation plots on its horizontal axis against the error rate.
/// </summary>
public enum CorrelationAxis
{
    /// <summary>Average (Farnsworth) speed, in WPM; every session has one.</summary>
    AverageSpeed,

    /// <summary>Signal-to-noise ratio, in dB (the negated noise level); only sessions with noise.</summary>
    SignalToNoise,

    /// <summary>Deepest QSB fade, in dB; only sessions with QSB.</summary>
    QsbDepth,
}

/// <summary>
/// One saved session placed on a correlation plane: the value of the plotted quantity it ran
/// at (speed, signal-to-noise ratio or QSB depth) against the error rate it produced.
/// </summary>
public sealed class ErrorCorrelationPoint
{
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// The session's value of the plotted quantity (WPM or dB, see <see cref="CorrelationAxis"/>):
    /// the horizontal coordinate.
    /// </summary>
    public double Value { get; init; }

    /// <summary>Error rate of the session, in percent: the vertical coordinate.</summary>
    public double ErrorRatePercent { get; init; }
}

/// <summary>
/// The scatter of one quantity against error rate over the analysis window, plus the
/// least-squares line fitted through it. <see cref="HasFit"/> is false when the window holds
/// fewer than two sessions or when every session shares one value (a vertical scatter has
/// no error-on-value line) or one error rate; <see cref="Slope"/>, <see cref="Intercept"/> and
/// <see cref="PearsonR"/> are then zero and carry no meaning.
/// </summary>
public sealed class ErrorCorrelation
{
    public CorrelationAxis Axis { get; init; }

    public IReadOnlyList<ErrorCorrelationPoint> Points { get; init; } = [];

    public bool HasFit { get; init; }

    /// <summary>Pearson correlation coefficient between the value and the error rate, -1 to 1.</summary>
    public double PearsonR { get; init; }

    /// <summary>Slope of the fitted line: error-rate percentage points per unit of the value.</summary>
    public double Slope { get; init; }

    /// <summary>Error rate, in percent, the fitted line predicts at a value of 0.</summary>
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
