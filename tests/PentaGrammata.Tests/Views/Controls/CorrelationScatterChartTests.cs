using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Models;
using PentaGrammata.Views.Controls;

namespace PentaGrammata.Tests.Views.Controls;

[TestClass]
public sealed class CorrelationScatterChartTests
{
    private static ErrorCorrelationPoint Point(double value) =>
        new() { RecordedAt = DateTimeOffset.UnixEpoch, Value = value, ErrorRatePercent = 1 };

    [TestMethod]
    public void GetValueScale_PositiveValues_StopsAtZero()
    {
        var (min, max) = CorrelationScatterChart.GetValueScale([Point(0.5), Point(3)]);

        Assert.AreEqual(0, min);
        Assert.AreEqual(5, max);
    }

    [TestMethod]
    public void GetValueScale_NegativeSnr_ReachesBelowZeroAndCoversEveryValue()
    {
        var (min, max) = CorrelationScatterChart.GetValueScale([Point(-7.5), Point(4)]);

        // Floor(-7.5) - 1 = -9; ceiling 4 + 1 = 5; a span of 14 rounded up to a multiple of 5.
        Assert.AreEqual(-9, min);
        Assert.AreEqual(6, max);
        Assert.AreEqual(0, (max - min) % 5);
    }

    [TestMethod]
    public void GetValueScale_AllNegative_StaysBelowZero()
    {
        var (min, max) = CorrelationScatterChart.GetValueScale([Point(-12), Point(-10)]);

        Assert.AreEqual(-13, min);
        Assert.AreEqual(-8, max);
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "No sessions in the analysis window.")]
    [DataRow(CorrelationAxis.SignalToNoise, "No sessions with noise in the analysis window.")]
    [DataRow(CorrelationAxis.QsbDepth, "No sessions with QSB in the analysis window.")]
    public void EmptyMessage_NamesTheSessionsTheAxisNeeds(CorrelationAxis axis, string expected)
    {
        Assert.AreEqual(expected, CorrelationScatterChart.EmptyMessage(axis));
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "Average speed (WPM)")]
    [DataRow(CorrelationAxis.SignalToNoise, "Signal-to-noise ratio (dB)")]
    [DataRow(CorrelationAxis.QsbDepth, "QSB fade depth (dB)")]
    public void AxisTitle_NamesTheQuantityAndUnit(CorrelationAxis axis, string expected)
    {
        Assert.AreEqual(expected, CorrelationScatterChart.AxisTitle(axis));
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "Average speed: 18 WPM")]
    [DataRow(CorrelationAxis.SignalToNoise, "Signal-to-noise ratio: -2.5 dB")]
    [DataRow(CorrelationAxis.QsbDepth, "QSB fade depth: 12 dB")]
    public void TooltipLines_LabelTheValueByAxis(CorrelationAxis axis, string valueLine)
    {
        var value = axis switch { CorrelationAxis.SignalToNoise => -2.5, CorrelationAxis.QsbDepth => 12, _ => 18 };
        var point = new ErrorCorrelationPoint { RecordedAt = DateTimeOffset.Now, Value = value, ErrorRatePercent = 4.25 };

        var lines = CorrelationScatterChart.TooltipLines(point, axis);

        Assert.HasCount(3, lines);
        Assert.AreEqual(point.RecordedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture), lines[0]);
        Assert.AreEqual(valueLine, lines[1]);
        Assert.AreEqual("Error rate: 4.25%", lines[2]);
    }
}
