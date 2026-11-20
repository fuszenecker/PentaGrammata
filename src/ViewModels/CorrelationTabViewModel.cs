using System;
using System.Globalization;

using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

/// <summary>
/// One tab of the correlation dialog: the scatter of one quantity (speed, signal-to-noise ratio
/// or QSB depth) against error rate, and the line describing it. The dialog owns the shared
/// analysis window and feeds every tab from the same records.
/// </summary>
public sealed class CorrelationTabViewModel : ViewModelBase
{
    private ErrorCorrelation _correlation;
    private string _summaryText = "Loading correlation data...";

    public CorrelationTabViewModel(CorrelationAxis axis, string header)
    {
        Axis = axis;
        Header = header;
        _correlation = new ErrorCorrelation { Axis = axis };
    }

    public CorrelationAxis Axis { get; }

    public string Header { get; }

    /// <summary>The scatter and its fitted line, rendered by the correlation chart.</summary>
    public ErrorCorrelation Correlation
    {
        get => _correlation;
        private set => SetProperty(ref _correlation, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    internal void Show(ErrorCorrelation correlation, double windowDays)
    {
        Correlation = correlation;
        SummaryText = BuildSummary(correlation, windowDays);
    }

    /// <summary>
    /// One line describing the window, the strength and direction of the correlation and the
    /// slope of the fit. Falls back to an explanatory message when there is nothing to fit,
    /// because an empty plot with no caption reads as a bug rather than as missing data.
    /// </summary>
    internal static string BuildSummary(ErrorCorrelation correlation, double windowDays)
    {
        var sessions = correlation.Points.Count;
        var which = correlation.Axis switch
        {
            CorrelationAxis.SignalToNoise => " with noise",
            CorrelationAxis.QsbDepth => " with QSB",
            _ => string.Empty,
        };
        var window = windowDays > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} session(s){1} in the last {2:0} day(s)", sessions, which, windowDays)
            : string.Format(CultureInfo.InvariantCulture, "{0} session(s){1} today", sessions, which);

        if (sessions == 0)
        {
            return $"{window}. Nothing to correlate yet.";
        }

        var quantity = correlation.Axis switch
        {
            CorrelationAxis.SignalToNoise => "signal-to-noise ratio",
            CorrelationAxis.QsbDepth => "QSB depth",
            _ => "speed",
        };

        if (!correlation.HasFit)
        {
            return sessions < 2
                ? $"{window}. At least two sessions are needed for a correlation."
                : $"{window}. No spread to correlate: every session ran at the same {quantity} or scored the same error rate.";
        }

        var unit = correlation.Axis == CorrelationAxis.AverageSpeed ? "WPM" : "dB";
        var summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0}. Correlation r = {1:0.00} ({2}), R² = {3:0.00}. Fit: error = {4:0.00} % {5} {6:0.000} %/{7}.",
            window,
            correlation.PearsonR,
            DescribeStrength(correlation.PearsonR),
            correlation.PearsonR * correlation.PearsonR,
            correlation.Intercept,
            correlation.Slope < 0 ? "-" : "+",
            Math.Abs(correlation.Slope),
            unit);

        if (correlation.ResidualStandardDeviation <= 0)
        {
            return summary;
        }

        return summary + string.Format(
            CultureInfo.InvariantCulture,
            " Band: ±1 σ = {0:0.00} percentage points around the line.",
            correlation.ResidualStandardDeviation);
    }

    private static string DescribeStrength(double r)
    {
        var direction = r < 0 ? "negative" : "positive";
        var magnitude = Math.Abs(r);
        var strength = magnitude switch
        {
            < 0.2 => "negligible",
            < 0.4 => "weak",
            < 0.6 => "moderate",
            < 0.8 => "strong",
            _ => "very strong",
        };

        return strength == "negligible" ? strength : $"{strength} {direction}";
    }
}
