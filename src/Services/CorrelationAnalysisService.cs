using System;
using System.Collections.Generic;
using System.Linq;

using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Pure implementation of <see cref="ICorrelationAnalysisService"/>. Windowing, the Pearson
/// coefficient and the least-squares fit live here so the view model is free of analysis math.
/// </summary>
public sealed class CorrelationAnalysisService : ICorrelationAnalysisService
{
    public ErrorCorrelation Analyze(IReadOnlyList<PracticeResultStatisticsRecord> records, CorrelationAxis axis, double windowDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(records);

        // A window of zero days means today only: from the start of the local calendar day,
        // not the last 24 hours, so yesterday evening's sessions are left out.
        var cutoff = windowDays > 0 ? now - TimeSpan.FromDays(windowDays) : StartOfLocalDay(now);

        var points = records
            .Where(r => r.RecordedAt >= cutoff && HasValue(r, axis))
            .OrderBy(r => r.RecordedAt)
            .Select(r => new ErrorCorrelationPoint
            {
                RecordedAt = r.RecordedAt,
                Value = ValueOf(r, axis),
                ErrorRatePercent = r.ErrorRatePercent,
            })
            .ToArray();

        if (points.Length < 2)
        {
            return new ErrorCorrelation { Axis = axis, Points = points };
        }

        var meanValue = points.Average(p => p.Value);
        var meanError = points.Average(p => p.ErrorRatePercent);

        var valueVariance = 0d;
        var errorVariance = 0d;
        var covariance = 0d;

        foreach (var point in points)
        {
            var dx = point.Value - meanValue;
            var dy = point.ErrorRatePercent - meanError;
            valueVariance += dx * dx;
            errorVariance += dy * dy;
            covariance += dx * dy;
        }

        // Every session at the same value leaves no horizontal spread to regress against, and
        // an identical error rate everywhere leaves the coefficient undefined (0/0). Both are
        // reported as "no fit" instead of dividing by zero.
        if (valueVariance <= 0 || errorVariance <= 0)
        {
            return new ErrorCorrelation { Axis = axis, Points = points };
        }

        var slope = covariance / valueVariance;
        var intercept = meanError - slope * meanValue;

        return new ErrorCorrelation
        {
            Axis = axis,
            Points = points,
            HasFit = true,
            PearsonR = Math.Clamp(covariance / Math.Sqrt(valueVariance * errorVariance), -1, 1),
            Slope = slope,
            Intercept = intercept,
            ResidualStandardDeviation = ResidualStandardDeviation(points, slope, intercept),
        };
    }

    /// <summary>Local midnight of the calendar day <paramref name="now"/> falls on.</summary>
    private static DateTimeOffset StartOfLocalDay(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local);
        var midnight = local.Date;
        return new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight));
    }

    /// <summary>
    /// Whether the session carries the quantity at all: every session has a speed, but the
    /// signal-to-noise ratio exists only when noise was mixed in, and the fade depth only when
    /// QSB was on. A session without it is left out rather than plotted at a made-up value.
    /// </summary>
    private static bool HasValue(PracticeResultStatisticsRecord record, CorrelationAxis axis) => axis switch
    {
        CorrelationAxis.SignalToNoise => record.NoiseType != NoiseType.None,
        CorrelationAxis.QsbDepth => record.QsbEnabled,
        _ => true,
    };

    /// <summary>
    /// The session's value on the axis: the average WPM, the signal-to-noise ratio (the negated
    /// noise level, as the settings show it) or the QSB fade depth.
    /// </summary>
    private static double ValueOf(PracticeResultStatisticsRecord record, CorrelationAxis axis) => axis switch
    {
        CorrelationAxis.SignalToNoise => -record.NoiseLevelDb,
        CorrelationAxis.QsbDepth => record.QsbDepthDb,
        _ => record.AverageWpm,
    };

    /// <summary>
    /// Spread of the sessions around the fitted line, as the standard error of the regression:
    /// the square root of the mean squared residual over n - 2 degrees of freedom. Two sessions
    /// leave no degrees of freedom (the line hits both exactly), so their spread is reported as
    /// zero and the chart draws no band.
    /// </summary>
    private static double ResidualStandardDeviation(IReadOnlyList<ErrorCorrelationPoint> points, double slope, double intercept)
    {
        if (points.Count <= 2)
        {
            return 0;
        }

        var sumOfSquares = 0d;
        foreach (var point in points)
        {
            var residual = point.ErrorRatePercent - (intercept + slope * point.Value);
            sumOfSquares += residual * residual;
        }

        return Math.Sqrt(sumOfSquares / (points.Count - 2));
    }
}
