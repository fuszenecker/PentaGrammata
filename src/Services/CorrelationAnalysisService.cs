using System;
using System.Collections.Generic;
using System.Linq;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Pure implementation of <see cref="ICorrelationAnalysisService"/>. Windowing, the Pearson
/// coefficient and the least-squares fit live here so the view model is free of analysis math.
/// </summary>
public sealed class CorrelationAnalysisService : ICorrelationAnalysisService
{
    public SpeedErrorCorrelation Analyze(IReadOnlyList<PracticeResultStatisticsRecord> records, double windowDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(records);

        // A non-positive window would select nothing; treat it as the smallest useful window
        // (the last day) rather than returning a misleading empty scatter.
        var cutoff = now - TimeSpan.FromDays(Math.Max(1d, windowDays));

        var points = records
            .Where(r => r.RecordedAt >= cutoff)
            .OrderBy(r => r.RecordedAt)
            .Select(r => new SpeedErrorPoint
            {
                RecordedAt = r.RecordedAt,
                AverageWpm = r.AverageWpm,
                ErrorRatePercent = r.ErrorRatePercent,
            })
            .ToArray();

        if (points.Length < 2)
        {
            return new SpeedErrorCorrelation { Points = points };
        }

        var meanWpm = points.Average(p => p.AverageWpm);
        var meanError = points.Average(p => p.ErrorRatePercent);

        var speedVariance = 0d;
        var errorVariance = 0d;
        var covariance = 0d;

        foreach (var point in points)
        {
            var dx = point.AverageWpm - meanWpm;
            var dy = point.ErrorRatePercent - meanError;
            speedVariance += dx * dx;
            errorVariance += dy * dy;
            covariance += dx * dy;
        }

        // Every session at the same speed leaves no horizontal spread to regress against, and
        // an identical error rate everywhere leaves the coefficient undefined (0/0). Both are
        // reported as "no fit" instead of dividing by zero.
        if (speedVariance <= 0 || errorVariance <= 0)
        {
            return new SpeedErrorCorrelation { Points = points };
        }

        var slope = covariance / speedVariance;
        var intercept = meanError - slope * meanWpm;

        return new SpeedErrorCorrelation
        {
            Points = points,
            HasFit = true,
            PearsonR = Math.Clamp(covariance / Math.Sqrt(speedVariance * errorVariance), -1, 1),
            Slope = slope,
            Intercept = intercept,
            ResidualStandardDeviation = ResidualStandardDeviation(points, slope, intercept),
        };
    }

    /// <summary>
    /// Spread of the sessions around the fitted line, as the standard error of the regression:
    /// the square root of the mean squared residual over n - 2 degrees of freedom. Two sessions
    /// leave no degrees of freedom (the line hits both exactly), so their spread is reported as
    /// zero and the chart draws no band.
    /// </summary>
    private static double ResidualStandardDeviation(IReadOnlyList<SpeedErrorPoint> points, double slope, double intercept)
    {
        if (points.Count <= 2)
        {
            return 0;
        }

        var sumOfSquares = 0d;
        foreach (var point in points)
        {
            var residual = point.ErrorRatePercent - (intercept + slope * point.AverageWpm);
            sumOfSquares += residual * residual;
        }

        return Math.Sqrt(sumOfSquares / (points.Count - 2));
    }
}
