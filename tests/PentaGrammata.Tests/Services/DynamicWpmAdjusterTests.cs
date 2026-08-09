using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class DynamicWpmAdjusterTests
{
    private const double Threshold = 10;

    // The auto-adjust tests use a 10 % threshold, so 3 % is comfortably below it and 30 %
    // well above; only ErrorRatePercent drives the adjustment.
    private const double PassRate = 3;
    private const double FailRate = 30;

    [TestMethod]
    public void Reset_SetsDynamicWpmFromConfigured()
    {
        var sut = CreateAdjuster();
        sut.Adjust(FailRate, Threshold, 3);
        sut.Adjust(FailRate, Threshold, 3);

        sut.Reset(25, 18);

        Assert.AreEqual(25, sut.DynamicCharacterWpm);
        Assert.AreEqual(18, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_WithErrorAboveThreshold_SlowsDownAverageWpm()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15);

        sut.Adjust(FailRate, Threshold, 3);

        // Only one error rate in the window (30 %), above the threshold: average drops
        // 15 -> 14 with the character WPM left unchanged.
        Assert.AreEqual(20, sut.DynamicCharacterWpm);
        Assert.AreEqual(14, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_WithErrorBelowThreshold_SpeedsUpAndRaisesCharacterWhenReached()
    {
        var sut = CreateAdjuster();
        // Start locked: average == character, so the first speed-up must raise both.
        sut.Reset(15, 15);

        sut.Adjust(PassRate, Threshold, 3);
        sut.Adjust(PassRate, Threshold, 3);

        // Two clean sessions: 15 -> 16 -> 17, with the character WPM raised alongside.
        Assert.AreEqual(17, sut.DynamicCharacterWpm);
        Assert.AreEqual(17, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_AveragesErrorRatesOverWindowRatherThanUsingLatestSession()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15);

        // Two flawless sessions: 15 -> 16 -> 17.
        sut.Adjust(0, Threshold, 3);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
        sut.Adjust(0, Threshold, 3);
        Assert.AreEqual(17, sut.DynamicAverageWpm);

        // Third session at 9 % passes on its own and the window average is (0 + 0 + 9) / 3 =
        // 3 %, so the speed keeps climbing.
        sut.Adjust(9, Threshold, 3);
        Assert.AreEqual(18, sut.DynamicAverageWpm);

        // Fourth session at 24 % pushes the window average to (0 + 9 + 24) / 3 = 11 %, above
        // the threshold, so the speed drops.
        sut.Adjust(24, Threshold, 3);
        Assert.AreEqual(17, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_WithFailingLatestSession_SlowsDownEvenWhenWindowAverageIsGood()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15);

        // Two flawless sessions: 15 -> 16 -> 17.
        sut.Adjust(0, Threshold, 3);
        sut.Adjust(0, Threshold, 3);
        Assert.AreEqual(17, sut.DynamicAverageWpm);

        // The window average is (0 + 0 + 24) / 3 = 8 %, below the 10 % threshold, so averaging
        // alone would speed up. The newest session failed at 24 %, and that veto wins.
        sut.Adjust(24, Threshold, 3);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
        Assert.AreEqual(20, sut.DynamicCharacterWpm);
    }

    [TestMethod]
    public void Adjust_KeepsOnlyTheLastWindowSizeErrorRates()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15);

        // A window of 1 means only the newest error rate ever counts.
        sut.Adjust(0, Threshold, 1);
        Assert.AreEqual(16, sut.DynamicAverageWpm);

        // The earlier 0 % is evicted, so the 24 % session alone drives the slow-down.
        sut.Adjust(24, Threshold, 1);
        Assert.AreEqual(15, sut.DynamicAverageWpm);

        sut.Adjust(0, Threshold, 1);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_NeverSlowsBelowOneWpm()
    {
        var sut = CreateAdjuster();
        sut.Reset(1, 1);

        sut.Adjust(FailRate, Threshold, 1);
        sut.Adjust(FailRate, Threshold, 1);

        Assert.AreEqual(1, sut.DynamicAverageWpm);
        Assert.AreEqual(1, sut.DynamicCharacterWpm);
    }

    private static DynamicWpmAdjuster CreateAdjuster()
        => new(Substitute.For<ILogger<DynamicWpmAdjuster>>());
}
