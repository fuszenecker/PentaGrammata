using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeControllerTests
{
    [TestMethod]
    public async Task StartAsync_GeneratesGroupsAndPlaysWithExpectedArguments()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.DefaultDurationMins = 2;
        config.Practice.AverageWpm = 20;
        config.Practice.CharacterWpm = 24;
        config.CharacterSets["Letters"] = "ABCDE";
        config.Practice.DefaultCharacterSet = "Letters";
        config.Audio.Noise.Type = NoiseType.Gaussian;
        config.Audio.Noise.LevelDb = -6;
        config.Audio.Noise.BandwidthHz = 400;
        config.Audio.Noise.AgcEnabled = false;
        config.Audio.Noise.AgcDelaySeconds = 0.75;
        config.Audio.Noise.ApfEnabled = false;
        config.Audio.Noise.ApfBandwidthHz = 80;
        config.Audio.Noise.ApfPeakGainDb = -6;
        configService.Current.Returns(config);

        morseGenerator.GenerateGroupsOf5("ABCDE", 28).Returns("ABCDE FGHIJ");

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        await sut.StartAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.LastGeneratedText);
        Assert.IsFalse(sut.IsPracticing);
        morseGenerator.Received(1).GenerateGroupsOf5("ABCDE", 28);
        await morsePlayer.Received(1).PlayMorseCodeAsync(
            "vvv = ABCDE FGHIJ <ar>",
            Arg.Is<MorsePlaybackSettings>(s =>
                s.CharacterWpm == 24 &&
                s.AverageWpm == 20 &&
                s.SampleRate == config.Audio.SampleRate &&
                s.Frequency == config.Audio.Frequency &&
                s.VolumeDb == config.Audio.VolumeDb &&
                s.BeepRampMs == config.Audio.BeepRampMs &&
                s.NoiseType == NoiseType.Gaussian &&
                s.NoiseLevelDb == -6 &&
                s.NoiseBandwidthHz == 400 &&
                s.AgcEnabled == false &&
                s.AgcDelaySeconds == 0.75 &&
                s.ApfEnabled == false &&
                s.ApfBandwidthHz == 80 &&
                s.ApfPeakGainDb == -6),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task StartAsync_WithConfiguredCustomText_SendsItVerbatimWithoutGenerating()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.CustomText = "CQ CQ DE HA5XYZ";
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        await sut.StartAsync();

        Assert.AreEqual("CQ CQ DE HA5XYZ", sut.LastGeneratedText);
        morseGenerator.DidNotReceiveWithAnyArgs().GenerateGroupsOf5(default!, default);
        await morsePlayer.Received(1).PlayMorseCodeAsync(
            "vvv = CQ CQ DE HA5XYZ <ar>",
            Arg.Any<MorsePlaybackSettings>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task StartAsync_WithMultiLineCustomText_CollapsesWhitespaceToWordGaps()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.CustomText = "  CQ  CQ\r\nDE   HA5XYZ\n";
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        await sut.StartAsync();

        Assert.AreEqual("CQ CQ DE HA5XYZ", sut.LastGeneratedText);
    }

    [TestMethod]
    public async Task StartAsync_WithBlankCustomText_FallsBackToGeneratedGroups()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.CustomText = "   ";
        config.Practice.DefaultDurationMins = 2;
        config.Practice.AverageWpm = 20;
        configService.Current.Returns(config);

        morseGenerator.GenerateGroupsOf5(Arg.Any<string>(), 28).Returns("ABCDE FGHIJ");

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        await sut.StartAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.LastGeneratedText);
        morseGenerator.Received(1).GenerateGroupsOf5("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 28);
    }

    [TestMethod]
    public void TryApplySettings_AppliesCustomText()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var loadedConfig = CreateDefaultConfiguration();
        loadedConfig.Practice.CustomText = "OLD TEXT";
        configService.Current.Returns(loadedConfig);
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        var newSettings = CreateDefaultConfiguration();
        newSettings.Practice.CustomText = "NEW TEXT";

        Assert.IsTrue(sut.TryApplySettings(newSettings, out _));
        Assert.AreEqual("NEW TEXT", loadedConfig.Practice.CustomText);
    }

    [TestMethod]
    public void BuildResult_DelegatesToEvaluator_AndStoresLastReceivedText()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.ErrorThreshold = 17.5;
        configService.Current.Returns(config);

        var expected = new PracticeResult
        {
            CharacterCount = 5,
            ErrorCount = 1,
            ErrorRatePercent = 20,
            IsSuccessful = false,
        };
        resultEvaluator.Evaluate(string.Empty, "RX", 17.5).Returns(expected);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        var actual = sut.BuildResult("RX");

        Assert.AreSame(expected, actual);
        Assert.AreEqual("RX", sut.LastReceivedText);
        resultEvaluator.Received(1).Evaluate(string.Empty, "RX", 17.5);
    }

    [TestMethod]
    public void TryApplySettings_WhenValidationFails_DoesNotSaveAndReturnsError()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        configService.Current.Returns(CreateDefaultConfiguration());
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "invalid settings";
                return false;
            });

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        var success = sut.TryApplySettings(CreateDefaultConfiguration(), out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("invalid settings", error);
        configService.DidNotReceive().RequestSave();
    }

    [TestMethod]
    public void TryApplySettings_WhenValid_UpdatesStateAndPersistsSnapshot()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var loadedConfig = CreateDefaultConfiguration();
        configService.Current.Returns(loadedConfig);
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);
        var newSettings = new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = 7,
                CharacterWpm = 30,
                AverageWpm = 18,
                DefaultCharacterSet = "Custom",
                ErrorThreshold = 12.5,
            },
            Audio = new Audio
            {
                SampleRate = 48000,
                Frequency = 700,
                VolumeDb = -10,
                BeepRampMs = 6,
                Noise = new NoiseSettings
                {
                    Type = NoiseType.Uniform,
                    LevelDb = -8,
                    BandwidthHz = 350,
                    AgcEnabled = false,
                    AgcDelaySeconds = 0.9,
                    ApfEnabled = false,
                    ApfBandwidthHz = 70,
                    ApfPeakGainDb = -4,
                },
            },
            CharacterSets = new CharacterSets
            {
                ["Custom"] = "ABCDE",
                ["Bad"] = "   ",
            },
        };

        var success = sut.TryApplySettings(newSettings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
        configService.Received(1).RequestSave();
        Assert.AreEqual(7, loadedConfig.Practice.DefaultDurationMins);
        Assert.AreEqual(30, loadedConfig.Practice.CharacterWpm);
        Assert.AreEqual(18, loadedConfig.Practice.AverageWpm);
        Assert.AreEqual("Custom", loadedConfig.Practice.DefaultCharacterSet);
        Assert.AreEqual(12.5, loadedConfig.Practice.ErrorThreshold);
        Assert.AreEqual(48000, loadedConfig.Audio.SampleRate);
        Assert.AreEqual(700, loadedConfig.Audio.Frequency);
        Assert.AreEqual(-10, loadedConfig.Audio.VolumeDb);
        Assert.AreEqual(6, loadedConfig.Audio.BeepRampMs);
        Assert.AreEqual(NoiseType.Uniform, loadedConfig.Audio.Noise.Type);
        Assert.AreEqual(-8, loadedConfig.Audio.Noise.LevelDb);
        Assert.AreEqual(350, loadedConfig.Audio.Noise.BandwidthHz);
        Assert.IsFalse(loadedConfig.Audio.Noise.AgcEnabled);
        Assert.AreEqual(0.9, loadedConfig.Audio.Noise.AgcDelaySeconds);
        Assert.IsFalse(loadedConfig.Audio.Noise.ApfEnabled);
        Assert.AreEqual(70, loadedConfig.Audio.Noise.ApfBandwidthHz);
        Assert.AreEqual(-4, loadedConfig.Audio.Noise.ApfPeakGainDb);
        Assert.HasCount(1, loadedConfig.CharacterSets);
        Assert.AreEqual("ABCDE", loadedConfig.CharacterSets["Custom"]);
    }

    [TestMethod]
    public async Task StartAsync_WithAutoAdjust_UsesConfiguredWpmAsDynamicStart()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        configService.Current.Returns(config);

        var sut = CreateController(configService);

        await sut.StartAsync();

        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(15, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_WithErrorAboveThreshold_SlowsDownDynamicAverageWpm()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Failed());

        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.BuildResult("rx");

        // Only one error rate in the window (30 %), which is above the 10 % threshold, so the
        // average WPM slows from 15 to 14 with the character WPM left unchanged.
        await sut.StartAsync();
        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(14, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_WithErrorBelowThreshold_SpeedsUpAndRaisesCharacterWpmWhenReached()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        // Start locked: average == character, so the very first speed-up must raise both.
        config.Practice.CharacterWpm = 15;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Passed());

        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        sut.BuildResult("rx");

        // Two clean sessions: 15 -> 16 -> 17, with character WPM raised alongside.
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedCharacterWpm);
        Assert.AreEqual(17, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_AveragesErrorRatesOverWindowRatherThanUsingLatestSession()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10)
            .Returns(WithErrorRate(0), WithErrorRate(0), WithErrorRate(9), WithErrorRate(24));

        var sut = CreateController(configService, evaluator);

        // Two flawless sessions: 15 -> 16 -> 17.
        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);

        // Third session at 9 % passes on its own and the window average is (0 + 0 + 9) / 3 =
        // 3 %, so the speed keeps climbing.
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(18, sut.LastUsedAverageWpm);

        // Fourth session at 24 % pushes the window average to (0 + 9 + 24) / 3 = 11 %, above
        // the threshold, so the speed drops.
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_WithFailingLatestSession_SlowsDownEvenWhenWindowAverageIsGood()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10)
            .Returns(WithErrorRate(0), WithErrorRate(0), WithErrorRate(24));

        var sut = CreateController(configService, evaluator);

        // Two flawless sessions: 15 -> 16 -> 17.
        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);

        // The window average is (0 + 0 + 24) / 3 = 8 %, below the 10 % threshold, so averaging
        // alone would speed up. The newest session failed at 24 %, and that veto wins.
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);
        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
    }

    [TestMethod]
    public async Task BuildResult_KeepsOnlyTheLastWindowSizeErrorRates()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        // A window of 1 means only the newest error rate ever counts.
        config.Practice.AutoAdjustWindowSize = 1;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10)
            .Returns(WithErrorRate(0), WithErrorRate(24), WithErrorRate(0));

        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);

        // The earlier 0 % is evicted, so the 24 % session alone drives the slow-down.
        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(15, sut.LastUsedAverageWpm);

        sut.BuildResult("rx");
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_AdjustsAtMostOncePerSession()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Failed());

        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.BuildResult("rx");
        // Reopening the result window for the same session must not slow down again.
        sut.BuildResult("rx");

        await sut.StartAsync();
        Assert.AreEqual(14, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_WithAutoAdjustOff_LeavesWpmAtConfigured()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = false;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Failed());

        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.StartAsync();

        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(15, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task TryApplySettings_ResetsDynamicWpmToConfigured()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 3;
        configService.Current.Returns(config);

        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Passed());

        var sut = CreateController(configService, evaluator, settingsValidator);

        // Speed up once (15 -> 16), then re-apply the same settings: dynamic must reset.
        await sut.StartAsync();
        sut.BuildResult("rx");
        Assert.IsTrue(sut.TryApplySettings(config.Clone(), out _));
        await sut.StartAsync();

        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(15, sut.LastUsedAverageWpm);
    }

    private static PracticeController CreateController(
        IConfigurationService configService,
        IPracticeResultEvaluator? resultEvaluator = null,
        IPracticeSettingsValidator? settingsValidator = null)
    {
        return new PracticeController(
            Substitute.For<IMorsePlayer>(),
            Substitute.For<IMorseGenerator>(),
            settingsValidator ?? Substitute.For<IPracticeSettingsValidator>(),
            resultEvaluator ?? Substitute.For<IPracticeResultEvaluator>(),
            configService,
            Substitute.For<ILogger<PracticeController>>());
    }

    // The auto-adjust tests use a 10 % threshold, so 3 % is comfortably below it and 30 % well
    // above; only ErrorRatePercent drives the adjustment.
    private static PracticeResult Passed() => WithErrorRate(3);

    private static PracticeResult Failed() => WithErrorRate(30);

    private static PracticeResult WithErrorRate(double errorRatePercent) => new()
    {
        ErrorRatePercent = errorRatePercent,
        IsSuccessful = errorRatePercent <= 10,
    };

    [TestMethod]
    public void Constructor_WhenCharacterSetsAreEmpty_AddsDefaultSet()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.CharacterSets.Clear();
        config.Practice.DefaultCharacterSet = null!;
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configService, logger);

        Assert.HasCount(1, sut.CharacterSets);
        Assert.AreEqual("Default", sut.CharacterSets[0].Key);
        Assert.AreEqual("Default", sut.SelectedCharacterSet);
    }

    private static AppConfig CreateDefaultConfiguration()
    {
        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = 5,
                CharacterWpm = 20,
                AverageWpm = 15,
                DefaultCharacterSet = "Default",
                ErrorThreshold = 10,
            },
            Audio = new Audio
            {
                SampleRate = 44100,
                Frequency = 523.25,
                VolumeDb = -3,
                BeepRampMs = 4,
            },
            CharacterSets = new CharacterSets
            {
                ["Default"] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            },
        };
    }
}
