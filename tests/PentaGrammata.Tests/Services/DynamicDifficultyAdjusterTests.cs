using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class DynamicDifficultyAdjusterTests
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
        sut.Adjust(FailRate, Threshold, 3, AutoAdjustTargets.WpmOnly);
        sut.Adjust(FailRate, Threshold, 3, AutoAdjustTargets.WpmOnly);

        sut.Reset(25, 18, 15, 10);

        Assert.AreEqual(25, sut.DynamicCharacterWpm);
        Assert.AreEqual(18, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_Pass_LowersSnrAndDeepensQsbByTheirSteps()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 12, 10);

        sut.Adjust(PassRate, Threshold, 1, new AutoAdjustTargets { Snr = true, SnrStepDb = 1.5, Qsb = true, QsbStepDb = 2 });

        Assert.AreEqual(10.5, sut.DynamicSnrDb);
        Assert.AreEqual(12, sut.DynamicQsbDepthDb);
        // The WPM is not a target, so it stays.
        Assert.AreEqual(15, sut.DynamicAverageWpm);
        Assert.AreEqual(20, sut.DynamicCharacterWpm);
    }

    [TestMethod]
    public void Adjust_Fail_RaisesSnrAndMakesQsbShallower()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 12, 10);

        sut.Adjust(FailRate, Threshold, 1, new AutoAdjustTargets { Wpm = true, Snr = true, SnrStepDb = 3, Qsb = true, QsbStepDb = 4 });

        Assert.AreEqual(15, sut.DynamicSnrDb);
        Assert.AreEqual(6, sut.DynamicQsbDepthDb);
        Assert.AreEqual(14, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_NewestSessionFailing_VetoesTheLevelsToo()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 12, 10);
        var targets = new AutoAdjustTargets { Snr = true, SnrStepDb = 1, Qsb = true, QsbStepDb = 1 };

        sut.Adjust(0, Threshold, 3, targets);
        sut.Adjust(0, Threshold, 3, targets);
        // Window average (0 + 0 + 12) / 3 = 4 % is clean, but the newest 12 % failed.
        sut.Adjust(12, Threshold, 3, targets);

        Assert.AreEqual(11, sut.DynamicSnrDb);
        Assert.AreEqual(11, sut.DynamicQsbDepthDb);
    }

    [TestMethod]
    public void Adjust_KeepsLevelsInsideTheSettingsRanges()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, DynamicDifficultyAdjuster.MinSnrDb + 1, DynamicDifficultyAdjuster.MinQsbDepthDb + 1);

        sut.Adjust(PassRate, Threshold, 1, new AutoAdjustTargets { Snr = true, SnrStepDb = 5, Qsb = true, QsbStepDb = 0.5 });
        Assert.AreEqual(DynamicDifficultyAdjuster.MinSnrDb, sut.DynamicSnrDb);

        sut.Adjust(FailRate, Threshold, 1, new AutoAdjustTargets { Qsb = true, QsbStepDb = 5 });
        Assert.AreEqual(DynamicDifficultyAdjuster.MinQsbDepthDb, sut.DynamicQsbDepthDb);

        sut.Reset(20, 15, DynamicDifficultyAdjuster.MaxSnrDb, DynamicDifficultyAdjuster.MaxQsbDepthDb);
        sut.Adjust(FailRate, Threshold, 1, new AutoAdjustTargets { Snr = true, SnrStepDb = 5 });
        sut.Adjust(PassRate, Threshold, 1, new AutoAdjustTargets { Qsb = true, QsbStepDb = 5 });
        Assert.AreEqual(DynamicDifficultyAdjuster.MaxSnrDb, sut.DynamicSnrDb);
        Assert.AreEqual(DynamicDifficultyAdjuster.MaxQsbDepthDb, sut.DynamicQsbDepthDb);
    }

    [TestMethod]
    public void Adjust_WithErrorAboveThreshold_SlowsDownAverageWpm()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 15, 10);

        sut.Adjust(FailRate, Threshold, 3, AutoAdjustTargets.WpmOnly);

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
        sut.Reset(15, 15, 15, 10);

        sut.Adjust(PassRate, Threshold, 3, AutoAdjustTargets.WpmOnly);
        sut.Adjust(PassRate, Threshold, 3, AutoAdjustTargets.WpmOnly);

        // Two clean sessions: 15 -> 16 -> 17, with the character WPM raised alongside.
        Assert.AreEqual(17, sut.DynamicCharacterWpm);
        Assert.AreEqual(17, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_AveragesErrorRatesOverWindowRatherThanUsingLatestSession()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 15, 10);

        // Two flawless sessions: 15 -> 16 -> 17.
        sut.Adjust(0, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
        sut.Adjust(0, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(17, sut.DynamicAverageWpm);

        // Third session at 9 % passes on its own and the window average is (0 + 0 + 9) / 3 =
        // 3 %, so the speed keeps climbing.
        sut.Adjust(9, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(18, sut.DynamicAverageWpm);

        // Fourth session at 24 % pushes the window average to (0 + 9 + 24) / 3 = 11 %, above
        // the threshold, so the speed drops.
        sut.Adjust(24, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(17, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_WithFailingLatestSession_SlowsDownEvenWhenWindowAverageIsGood()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 15, 10);

        // Two flawless sessions: 15 -> 16 -> 17.
        sut.Adjust(0, Threshold, 3, AutoAdjustTargets.WpmOnly);
        sut.Adjust(0, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(17, sut.DynamicAverageWpm);

        // The window average is (0 + 0 + 24) / 3 = 8 %, below the 10 % threshold, so averaging
        // alone would speed up. The newest session failed at 24 %, and that veto wins.
        sut.Adjust(24, Threshold, 3, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
        Assert.AreEqual(20, sut.DynamicCharacterWpm);
    }

    [TestMethod]
    public void Adjust_KeepsOnlyTheLastWindowSizeErrorRates()
    {
        var sut = CreateAdjuster();
        sut.Reset(20, 15, 15, 10);

        // A window of 1 means only the newest error rate ever counts.
        sut.Adjust(0, Threshold, 1, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(16, sut.DynamicAverageWpm);

        // The earlier 0 % is evicted, so the 24 % session alone drives the slow-down.
        sut.Adjust(24, Threshold, 1, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(15, sut.DynamicAverageWpm);

        sut.Adjust(0, Threshold, 1, AutoAdjustTargets.WpmOnly);
        Assert.AreEqual(16, sut.DynamicAverageWpm);
    }

    [TestMethod]
    public void Adjust_NeverSlowsBelowOneWpm()
    {
        var sut = CreateAdjuster();
        sut.Reset(1, 1, 15, 10);

        sut.Adjust(FailRate, Threshold, 1, AutoAdjustTargets.WpmOnly);
        sut.Adjust(FailRate, Threshold, 1, AutoAdjustTargets.WpmOnly);

        Assert.AreEqual(1, sut.DynamicAverageWpm);
        Assert.AreEqual(1, sut.DynamicCharacterWpm);
    }

    private static DynamicDifficultyAdjuster CreateAdjuster()
        => new(Substitute.For<ILogger<DynamicDifficultyAdjuster>>());
}
