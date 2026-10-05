using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.Players;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class AudioHeadroomAnalyzerTests
{
    private static AudioHeadroomAnalyzer CreateAnalyzer() =>
        new(new MorseSignalRendererFactory(new NoiseGeneratorFactory()));

    private static MorsePlaybackSettings Settings() => new()
    {
        CharacterWpm = 20,
        AverageWpm = 20,
        SampleRate = 8000,
        Frequency = 600,
        VolumeDb = -3,
        BeepRampMs = 4,
    };

    [TestMethod]
    public void Analyze_CleanToneAtFullScale_ReportsNoClipping()
    {
        // With noise off nothing is added to the keyed tone, so 0 dBFS volume peaks at exactly
        // full scale: the loudest legal setting must not be reported as distorting.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with { VolumeDb = 0, NoiseType = NoiseType.None });

        Assert.IsFalse(report.WillClip, $"peak was {report.PeakDbFs:F2} dBFS");
        Assert.AreEqual(0.0, report.ClippedSampleRatio);
        Assert.AreEqual(0.0, report.PeakDbFs, 0.01);
    }

    [TestMethod]
    public void Analyze_QuietToneWithoutNoise_ReportsThePeakAsTheVolume()
    {
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with { VolumeDb = -12, NoiseType = NoiseType.None });

        Assert.IsFalse(report.WillClip);
        Assert.AreEqual(-12.0, report.PeakDbFs, 0.2);
    }

    [TestMethod]
    public void Analyze_FullVolumeWithLoudNoiseAndHeavyAgcBoost_ReportsClipping()
    {
        // 0 dBFS leaves no room for the noise the AGC then boosts on top of the tone.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with
        {
            VolumeDb = 0,
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -3,
            NoiseBandwidthHz = 2000,
            AgcEnabled = true,
            AgcMaxGainDb = 40,
            ApfEnabled = true,
            ApfPeakGainDb = 20,
        });

        Assert.IsTrue(report.WillClip, $"peak was {report.PeakDbFs:F2} dBFS");
        Assert.IsGreaterThan(0.0, report.ClippedSampleRatio);
    }

    [TestMethod]
    public void Analyze_LoweringTheVolume_LowersThePeak()
    {
        var sut = CreateAnalyzer();
        var noisy = Settings() with { NoiseType = NoiseType.Gaussian, NoiseLevelDb = -6, AgcEnabled = false, ApfEnabled = false };

        var loud = sut.Analyze(noisy with { VolumeDb = -3 });
        var quiet = sut.Analyze(noisy with { VolumeDb = -20 });

        Assert.IsLessThan(loud.PeakDbFs, quiet.PeakDbFs);
    }

    [TestMethod]
    public void Analyze_WithoutQsb_MeasuresTheShortProbe()
    {
        // Two PARIS words at 20 wpm: 60 / 20 seconds each.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings());

        Assert.AreEqual(6.0, report.ProbeSeconds, 0.1);
    }

    [TestMethod]
    public void Analyze_WithSlowQsb_MeasuresAtLeastTwoFadePeriods()
    {
        // A 20 s fade period is invisible in a 6 s probe, so the probe has to grow with it:
        // one period would only just reach the first deep fade, two also catch the recovery.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with { QsbEnabled = true, QsbPeriodSeconds = 20 });

        Assert.IsGreaterThanOrEqualTo(40.0, report.ProbeSeconds);
    }

    [TestMethod]
    public void Analyze_WithFastQsb_KeepsTheShortProbe()
    {
        // Two 1 s periods fit inside the two-word probe already; don't render more than needed.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with { QsbEnabled = true, QsbPeriodSeconds = 1 });

        Assert.AreEqual(6.0, report.ProbeSeconds, 0.1);
    }

    [TestMethod]
    public void Analyze_WithAbsurdQsbPeriod_CapsTheProbeLength()
    {
        // A hand-edited config must not turn an analysis that runs on every keystroke into a
        // minutes-long render.
        var sut = CreateAnalyzer();

        var report = sut.Analyze(Settings() with { QsbEnabled = true, QsbPeriodSeconds = 3600 });

        Assert.IsLessThan(60.0, report.ProbeSeconds);
    }

    [TestMethod]
    public void Analyze_WithSlowQsb_SeesTheFadeAndTheAgcBoostThatFollowsIt()
    {
        // The point of covering the fade: a faded signal lets the AGC wind its gain up, and the
        // recovery out of the fade then arrives into that raised gain. A probe too short to
        // contain the fade reports the unfaded peak instead.
        var sut = CreateAnalyzer();
        var faded = Settings() with
        {
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -15,
            AgcEnabled = true,
            AgcMaxGainDb = 18,
            QsbEnabled = true,
            QsbDepthDb = 30,
            QsbPeriodSeconds = 20,
        };

        var report = sut.Analyze(faded);

        Assert.IsGreaterThanOrEqualTo(40.0, report.ProbeSeconds);
        Assert.IsTrue(report.WillClip, $"peak was {report.PeakDbFs:F2} dBFS over {report.ProbeSeconds:F1} s");
    }

    [TestMethod]
    public void Analyze_SameSettingsTwice_ReportsTheSameVerdict()
    {
        // The probe is seeded, so the warning must not flicker while the dialog is open.
        var sut = CreateAnalyzer();
        var settings = Settings() with { NoiseType = NoiseType.Pink, NoiseLevelDb = -3, QsbEnabled = true };

        var first = sut.Analyze(settings);
        var second = sut.Analyze(settings);

        Assert.AreEqual(first, second);
    }
}
