using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class CorrelationAnalysisServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly CorrelationAnalysisService _sut = new();

    [TestMethod]
    public void Analyze_WithNoRecords_ReturnsEmptyScatterWithoutFit()
    {
        var result = _sut.Analyze([], CorrelationAxis.AverageSpeed, 10, Now);

        Assert.IsEmpty(result.Points);
        Assert.IsFalse(result.HasFit);
    }

    [TestMethod]
    public void Analyze_ExcludesRecordsOlderThanTheWindow()
    {
        var records = new[]
        {
            Record(Now.AddDays(-11), 18, 4),
            Record(Now.AddDays(-9), 20, 6),
            Record(Now.AddDays(-1), 22, 8),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        CollectionAssert.AreEqual(
            new[] { 20d, 22d },
            result.Points.Select(p => p.Value).ToArray());
    }

    [TestMethod]
    public void Analyze_OrdersPointsByRecordedAt()
    {
        var records = new[]
        {
            Record(Now.AddDays(-1), 22, 8),
            Record(Now.AddDays(-5), 18, 3),
            Record(Now.AddDays(-3), 20, 5),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        CollectionAssert.AreEqual(
            new[] { 18d, 20d, 22d },
            result.Points.Select(p => p.Value).ToArray());
    }

    [TestMethod]
    public void Analyze_WithSingleSession_ReturnsThePointButNoFit()
    {
        var result = _sut.Analyze([Record(Now.AddDays(-1), 20, 5)], CorrelationAxis.AverageSpeed, 10, Now);

        Assert.HasCount(1, result.Points);
        Assert.IsFalse(result.HasFit);
        Assert.AreEqual(0, result.PearsonR);
        Assert.AreEqual(0, result.Slope);
    }

    [TestMethod]
    public void Analyze_WithIdenticalSpeeds_ReturnsNoFit()
    {
        var records = new[]
        {
            Record(Now.AddDays(-3), 20, 4),
            Record(Now.AddDays(-2), 20, 9),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        Assert.HasCount(2, result.Points);
        Assert.IsFalse(result.HasFit);
    }

    [TestMethod]
    public void Analyze_WithIdenticalErrorRates_ReturnsNoFit()
    {
        var records = new[]
        {
            Record(Now.AddDays(-3), 18, 5),
            Record(Now.AddDays(-2), 24, 5),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        Assert.IsFalse(result.HasFit);
    }

    [TestMethod]
    public void Analyze_WithPerfectlyRisingErrorRate_ReturnsUnitCorrelationAndExactLine()
    {
        // Error rate is exactly 2 % per WPM above a 10 % offset, so the fit must reproduce it.
        var records = new[]
        {
            Record(Now.AddDays(-4), 15, 20),
            Record(Now.AddDays(-3), 18, 26),
            Record(Now.AddDays(-2), 21, 32),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        Assert.IsTrue(result.HasFit);
        Assert.AreEqual(1.0, result.PearsonR, 1e-9);
        Assert.AreEqual(2.0, result.Slope, 1e-9);
        Assert.AreEqual(-10.0, result.Intercept, 1e-9);
        Assert.AreEqual(0.0, result.ResidualStandardDeviation, 1e-9);
    }

    [TestMethod]
    public void Analyze_WithTwoSessions_ReportsNoResidualSpread()
    {
        // The line passes exactly through both points, so there is no degree of freedom left
        // to measure spread with.
        var records = new[]
        {
            Record(Now.AddDays(-3), 18, 3),
            Record(Now.AddDays(-2), 22, 9),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        Assert.IsTrue(result.HasFit);
        Assert.AreEqual(0.0, result.ResidualStandardDeviation, 1e-9);
    }

    [TestMethod]
    public void Analyze_WhenFasterSessionsScoreBetter_ReturnsNegativeCorrelation()
    {
        var records = new[]
        {
            Record(Now.AddDays(-4), 15, 12),
            Record(Now.AddDays(-3), 18, 9),
            Record(Now.AddDays(-2), 21, 4),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        Assert.IsTrue(result.HasFit);
        Assert.IsLessThan(0, result.PearsonR);
        Assert.IsLessThan(0, result.Slope);
    }

    [TestMethod]
    public void Analyze_WithScatteredData_MatchesLeastSquaresResult()
    {
        var records = new[]
        {
            Record(Now.AddDays(-4), 10, 5),
            Record(Now.AddDays(-3), 20, 8),
            Record(Now.AddDays(-2), 30, 7),
            Record(Now.AddDays(-1), 40, 12),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 10, Now);

        // Hand-computed: mean speed 25, mean error 8, covariance sum 100, speed variance 500,
        // error variance 26, so slope = 0.2, intercept = 3 and r = 100 / sqrt(13000).
        Assert.AreEqual(0.2, result.Slope, 1e-9);
        Assert.AreEqual(3.0, result.Intercept, 1e-9);
        Assert.AreEqual(0.877, result.PearsonR, 0.001);

        // The line predicts 5, 7, 9 and 11 %, so the residuals are 0, 1, -2 and 1: a sum of
        // squares of 6 over two degrees of freedom.
        Assert.AreEqual(Math.Sqrt(3), result.ResidualStandardDeviation, 1e-9);
    }

    [TestMethod]
    public void Analyze_WithNonPositiveWindow_FallsBackToTheLastDay()
    {
        var records = new[]
        {
            Record(Now.AddDays(-2), 18, 4),
            Record(Now.AddHours(-2), 22, 9),
        };

        var result = _sut.Analyze(records, CorrelationAxis.AverageSpeed, 0, Now);

        Assert.HasCount(1, result.Points);
        Assert.AreEqual(22, result.Points[0].Value);
    }

    [TestMethod]
    public void Analyze_SignalToNoise_UsesNegatedNoiseLevelAndSkipsSessionsWithoutNoise()
    {
        var records = new[]
        {
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-3), AverageWpm = 20, ErrorRatePercent = 2, NoiseType = NoiseType.Gaussian, NoiseLevelDb = -20 },
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-2), AverageWpm = 20, ErrorRatePercent = 9, NoiseType = NoiseType.None, NoiseLevelDb = -5 },
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-1), AverageWpm = 20, ErrorRatePercent = 8, NoiseType = NoiseType.Pink, NoiseLevelDb = -5 },
        };

        var result = _sut.Analyze(records, CorrelationAxis.SignalToNoise, 10, Now);

        Assert.AreEqual(CorrelationAxis.SignalToNoise, result.Axis);
        CollectionAssert.AreEqual(new[] { 20d, 5d }, result.Points.Select(p => p.Value).ToArray());
        Assert.IsTrue(result.HasFit);
        // Lower SNR, more errors: the slope is negative.
        Assert.IsLessThan(0, result.Slope);
    }

    [TestMethod]
    public void Analyze_QsbDepth_UsesFadeDepthAndSkipsSessionsWithoutQsb()
    {
        var records = new[]
        {
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-3), ErrorRatePercent = 2, QsbEnabled = true, QsbDepthDb = 6 },
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-2), ErrorRatePercent = 9, QsbEnabled = false, QsbDepthDb = 30 },
            new PracticeResultStatisticsRecord { RecordedAt = Now.AddDays(-1), ErrorRatePercent = 8, QsbEnabled = true, QsbDepthDb = 18 },
        };

        var result = _sut.Analyze(records, CorrelationAxis.QsbDepth, 10, Now);

        CollectionAssert.AreEqual(new[] { 6d, 18d }, result.Points.Select(p => p.Value).ToArray());
        Assert.AreEqual(0.5, result.Slope, 1e-9);
    }

    private static PracticeResultStatisticsRecord Record(DateTimeOffset recordedAt, int averageWpm, double errorRatePercent)
    {
        return new PracticeResultStatisticsRecord
        {
            RecordedAt = recordedAt,
            CharacterWpm = Math.Max(averageWpm, 20),
            AverageWpm = averageWpm,
            ErrorRatePercent = errorRatePercent,
            ErrorThresholdPercent = 5,
        };
    }
}
