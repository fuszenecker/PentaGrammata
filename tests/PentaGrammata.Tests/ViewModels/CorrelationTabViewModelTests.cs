using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class CorrelationTabViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private static ErrorCorrelationPoint Point(double value, double error) =>
        new() { RecordedAt = Now, Value = value, ErrorRatePercent = error };

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "0 session(s) in the last 10 day(s). Nothing to correlate yet.")]
    [DataRow(CorrelationAxis.SignalToNoise, "0 session(s) with noise in the last 10 day(s). Nothing to correlate yet.")]
    [DataRow(CorrelationAxis.QsbDepth, "0 session(s) with QSB in the last 10 day(s). Nothing to correlate yet.")]
    public void BuildSummary_Empty_NamesTheSessionsTheAxisNeeds(CorrelationAxis axis, string expected)
    {
        Assert.AreEqual(expected, CorrelationTabViewModel.BuildSummary(new ErrorCorrelation { Axis = axis }, 10));
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "0 session(s) today. Nothing to correlate yet.")]
    [DataRow(CorrelationAxis.SignalToNoise, "0 session(s) with noise today. Nothing to correlate yet.")]
    [DataRow(CorrelationAxis.QsbDepth, "0 session(s) with QSB today. Nothing to correlate yet.")]
    public void BuildSummary_ZeroDayWindow_SaysToday(CorrelationAxis axis, string expected)
    {
        Assert.AreEqual(expected, CorrelationTabViewModel.BuildSummary(new ErrorCorrelation { Axis = axis }, 0));
    }

    [TestMethod]
    public void BuildSummary_SingleSession_AsksForAnother()
    {
        var correlation = new ErrorCorrelation { Axis = CorrelationAxis.SignalToNoise, Points = [Point(12, 3)] };

        Assert.AreEqual(
            "1 session(s) with noise today. At least two sessions are needed for a correlation.",
            CorrelationTabViewModel.BuildSummary(correlation, 0));
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "speed")]
    [DataRow(CorrelationAxis.SignalToNoise, "signal-to-noise ratio")]
    [DataRow(CorrelationAxis.QsbDepth, "QSB depth")]
    public void BuildSummary_NoSpread_NamesTheQuantity(CorrelationAxis axis, string quantity)
    {
        var correlation = new ErrorCorrelation { Axis = axis, Points = [Point(10, 3), Point(10, 5)] };

        StringAssert.EndsWith(
            CorrelationTabViewModel.BuildSummary(correlation, 5),
            $"No spread to correlate: every session ran at the same {quantity} or scored the same error rate.");
    }

    [TestMethod]
    [DataRow(CorrelationAxis.AverageSpeed, "%/WPM.")]
    [DataRow(CorrelationAxis.SignalToNoise, "%/dB.")]
    [DataRow(CorrelationAxis.QsbDepth, "%/dB.")]
    public void BuildSummary_Fit_UsesTheAxisUnit(CorrelationAxis axis, string unit)
    {
        var correlation = new ErrorCorrelation
        {
            Axis = axis,
            Points = [Point(10, 3), Point(20, 5)],
            HasFit = true,
            PearsonR = -0.5,
            Slope = -0.25,
            Intercept = 8,
        };

        var summary = CorrelationTabViewModel.BuildSummary(correlation, 0);

        StringAssert.StartsWith(summary, "2 session(s)");
        StringAssert.Contains(summary, " today. Correlation r = -0.50 (moderate negative), R² = 0.25.");
        StringAssert.EndsWith(summary, $"Fit: error = 8.00 % - 0.250 {unit}");
    }

    [TestMethod]
    public void Show_UpdatesCorrelationAndSummary()
    {
        var sut = new CorrelationTabViewModel(CorrelationAxis.QsbDepth, "QSB depth");
        var correlation = new ErrorCorrelation { Axis = CorrelationAxis.QsbDepth, Points = [Point(6, 2)] };

        sut.Show(correlation, 3);

        Assert.AreSame(correlation, sut.Correlation);
        StringAssert.StartsWith(sut.SummaryText, "1 session(s) with QSB in the last 3 day(s).");
    }
}
