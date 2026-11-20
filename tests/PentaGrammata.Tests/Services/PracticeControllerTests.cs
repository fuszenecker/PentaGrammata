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
        config.Audio.Noise.QsbEnabled = true;
        config.Audio.Noise.QsbDepthDb = 12;
        config.Audio.Noise.QsbPeriodSeconds = 6.5;
        configService.Current.Returns(config);

        morseGenerator.GenerateGroupsOf5("ABCDE", 28).Returns("ABCDE FGHIJ");

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

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
                s.ApfPeakGainDb == -6 &&
                s.QsbEnabled == true &&
                s.QsbDepthDb == 12 &&
                s.QsbPeriodSeconds == 6.5),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task StartAsync_WithCustomTextTopic_SendsItVerbatimWithoutGenerating()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.CustomText;
        config.Practice.CustomText = "CQ CQ DE HA5XYZ";
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        await sut.StartAsync();

        // One line is one item, its words joined like a multi-word city.
        Assert.AreEqual("CQ_CQ_DE_HA5XYZ", sut.LastGeneratedText);
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
        config.Practice.Topic = PracticeTopic.CustomText;
        config.Practice.CustomText = "  CQ  CQ\r\nDE   HA5XYZ\n";
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        await sut.StartAsync();

        // Each line is one item; whitespace inside a line collapses to one word gap.
        Assert.AreEqual("CQ_CQ DE_HA5XYZ", sut.LastGeneratedText);
    }

    [TestMethod]
    public async Task StartAsync_WithGroupsTopic_IgnoresCustomTextAndGeneratesGroups()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.Groups;
        config.Practice.CustomText = "CQ CQ";
        config.Practice.DefaultDurationMins = 2;
        config.Practice.AverageWpm = 20;
        configService.Current.Returns(config);

        morseGenerator.GenerateGroupsOf5(Arg.Any<string>(), 28).Returns("ABCDE FGHIJ");

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        await sut.StartAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.LastGeneratedText);
        morseGenerator.Received(1).GenerateGroupsOf5("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 28);
    }

    [TestMethod]
    public async Task StartAsync_WithCitiesTopic_SendsCitiesFromTheListForTheDuration()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var configService = Substitute.For<IConfigurationService>();

        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.Cities;
        config.Practice.DefaultDurationMins = 1;
        config.Practice.AverageWpm = 10;
        config.Topics.Cities = ["Budapest", "Wien"];
        configService.Current.Returns(config);

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator),
            Substitute.For<IPracticeSettingsValidator>(), Substitute.For<IPracticeResultEvaluator>(), configService,
            CreateAdjuster(), Substitute.For<ILogger<PracticeController>>());

        await sut.StartAsync();

        var words = sut.LastGeneratedText.Split(' ');
        Assert.IsTrue(words.All(w => w is "BUDAPEST" or "WIEN"), sut.LastGeneratedText);
        // 1 min at 10 WPM is worth 7 groups, i.e. 35 characters, every repetition played included.
        var played = (string)morsePlayer.ReceivedCalls().Single().GetArguments()[0]!;
        Assert.IsGreaterThanOrEqualTo(35, played["vvv = ".Length..^" <ar>".Length].Replace(" ", string.Empty).Length, played);
        morseGenerator.DidNotReceiveWithAnyArgs().GenerateGroupsOf5(default!, default);
    }

    [TestMethod]
    public async Task StartAsync_PlaysMultiWordItemsWithOrdinaryWordGaps()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.Cities;
        config.Practice.DefaultDurationMins = 1;
        config.Practice.AverageWpm = 1;
        config.Topics.Cities = ["NEW YORK"];
        configService.Current.Returns(config);
        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            Substitute.For<IPracticeSettingsValidator>(), Substitute.For<IPracticeResultEvaluator>(), configService,
            CreateAdjuster(), Substitute.For<ILogger<PracticeController>>());

        await sut.StartAsync();

        // A city is played twice in a row by default, but checked once.
        Assert.AreEqual("NEW_YORK", sut.LastGeneratedText);
        await morsePlayer.Received(1).PlayMorseCodeAsync("vvv = NEW YORK NEW YORK <ar>", Arg.Any<MorsePlaybackSettings>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task StartAsync_PlaysEveryRepetition_ButChecksEachItemOnce()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.CustomText;
        config.Practice.CustomText = "CQ DE HA5XYZ\nNAME LACI";
        config.Topics.Repetitions.CustomText = new Repetition(2, 2);
        configService.Current.Returns(config);
        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            Substitute.For<IPracticeSettingsValidator>(), evaluator, configService,
            CreateAdjuster(), Substitute.For<ILogger<PracticeController>>());

        await sut.StartAsync();
        await sut.BuildResultAsync("CQ DE HA5XYZ NAME LACI");

        // Each line is repeated as a whole, never word by word.
        await morsePlayer.Received(1).PlayMorseCodeAsync("vvv = CQ DE HA5XYZ CQ DE HA5XYZ NAME LACI NAME LACI <ar>", Arg.Any<MorsePlaybackSettings>(), Arg.Any<CancellationToken>());
        Assert.AreEqual("CQ_DE_HA5XYZ NAME_LACI", sut.LastGeneratedText, "Revealed, checked and saved once.");
        evaluator.Received(1).Evaluate("CQ_DE_HA5XYZ NAME_LACI", "CQ DE HA5XYZ NAME LACI", Arg.Any<double>());
    }

    [TestMethod]
    public void SelectedTopic_IsReadFromAndWrittenToTheConfiguration()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.Topic = PracticeTopic.Names;
        configService.Current.Returns(config);
        var sut = new PracticeController(Substitute.For<IMorsePlayer>(), new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            Substitute.For<IPracticeSettingsValidator>(), Substitute.For<IPracticeResultEvaluator>(), configService,
            CreateAdjuster(), Substitute.For<ILogger<PracticeController>>());

        Assert.AreEqual(PracticeTopic.Names, sut.SelectedTopic);
        sut.SelectedTopic = PracticeTopic.Mixed;
        configService.Received(1).SetSelectedTopic(PracticeTopic.Mixed);
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

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        var newSettings = CreateDefaultConfiguration();
        newSettings.Practice.CustomText = "NEW TEXT";

        Assert.IsTrue(sut.TryApplySettings(newSettings, out _));
        // The controller delegates the wholesale copy to the configuration owner rather than
        // mutating Current itself.
        configService.Received(1).ApplyPracticeSettings(Arg.Is<AppConfig>(c => c.Practice.CustomText == "NEW TEXT"));
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

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        var actual = sut.BuildResult("RX");

        Assert.AreSame(expected, actual);
        Assert.AreEqual("RX", sut.LastReceivedText);
        resultEvaluator.Received(1).Evaluate(string.Empty, "RX", 17.5);
    }

    [TestMethod]
    public void TryApplySettings_WhenValidationFails_DoesNotApplyAndReturnsError()
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

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);

        var success = sut.TryApplySettings(CreateDefaultConfiguration(), out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("invalid settings", error);
        configService.DidNotReceive().ApplyPracticeSettings(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public void TryApplySettings_WhenValid_DelegatesToConfigServiceAndDoesNotPersistDirectly()
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

        var sut = new PracticeController(morsePlayer, new PracticeTextGeneratorFactory(morseGenerator), settingsValidator, resultEvaluator, configService, CreateAdjuster(), logger);
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
        // The controller hands the snapshot to the configuration owner; it must not reach into
        // Current or persist directly.
        configService.Received(1).ApplyPracticeSettings(newSettings);
        configService.DidNotReceive().RequestSave();
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
    public async Task RecordSavedResult_WithAutoAdjustSnrAndQsb_MovesLevelsByTheirStepsAndPlaysThem()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustSnr = true;
        config.Practice.AutoAdjustSnrStepDb = 2;
        config.Practice.AutoAdjustQsb = true;
        config.Practice.AutoAdjustQsbStepDb = 3;
        config.Audio.Noise.Type = NoiseType.Gaussian;
        config.Audio.Noise.LevelDb = -15;
        config.Audio.Noise.QsbEnabled = true;
        config.Audio.Noise.QsbDepthDb = 10;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Passed());
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var sut = new PracticeController(
            morsePlayer,
            new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            Substitute.For<IPracticeSettingsValidator>(),
            evaluator,
            configService,
            CreateAdjuster(),
            Substitute.For<ILogger<PracticeController>>());

        await sut.StartAsync();
        sut.RecordSavedResult(sut.BuildResult("rx"));

        // A pass makes it harder: SNR 15 -> 13 dB (noise level -15 -> -13), fade 10 -> 13 dB.
        // The WPM does not auto-adjust, so it stays where it was configured.
        Assert.AreEqual(13, sut.CurrentSnrDb);
        Assert.AreEqual(13, sut.CurrentQsbDepthDb);
        Assert.AreEqual(config.Practice.AverageWpm, sut.CurrentAverageWpm);

        await sut.StartAsync();
        Assert.AreEqual(-13, sut.LastUsedNoise.LevelDb);
        Assert.AreEqual(13, sut.LastUsedNoise.QsbDepthDb);
        await morsePlayer.Received().PlayMorseCodeAsync(
            Arg.Any<string>(),
            Arg.Is<MorsePlaybackSettings>(m => m.NoiseLevelDb == -13 && m.QsbDepthDb == 13),
            Arg.Any<CancellationToken>());

        // Like the WPM, the adjusted levels live in memory only: the configuration is untouched.
        Assert.AreEqual(-15, config.Audio.Noise.LevelDb);
        Assert.AreEqual(10, config.Audio.Noise.QsbDepthDb);
    }

    [TestMethod]
    public async Task RecordSavedResult_WithAutoAdjustSnrButNoNoise_LeavesTheLevelAlone()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustSnr = true;
        config.Practice.AutoAdjustQsb = true;
        config.Audio.Noise.Type = NoiseType.None;
        config.Audio.Noise.QsbEnabled = false;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Failed());
        var sut = CreateController(configService, evaluator);

        await sut.StartAsync();
        sut.RecordSavedResult(sut.BuildResult("rx"));

        Assert.IsNull(sut.CurrentSnrDb);
        Assert.IsNull(sut.CurrentQsbDepthDb);

        // Turning noise on later starts from the configured level, not from a silently drifted one.
        config.Audio.Noise.Type = NoiseType.Pink;
        Assert.AreEqual(-config.Audio.Noise.LevelDb, sut.CurrentSnrDb);
    }

    [TestMethod]
    public async Task RecordSavedResult_WithErrorAboveThreshold_SlowsDownDynamicAverageWpm()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));

        // Only one error rate in the window (30 %), which is above the 10 % threshold, so the
        // average WPM slows from 15 to 14 with the character WPM left unchanged.
        await sut.StartAsync();
        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(14, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_WithErrorBelowThreshold_SpeedsUpAndRaisesCharacterWpmWhenReached()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        sut.RecordSavedResult(sut.BuildResult("rx"));

        // Two clean sessions: 15 -> 16 -> 17, with character WPM raised alongside.
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedCharacterWpm);
        Assert.AreEqual(17, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_AveragesErrorRatesOverWindowRatherThanUsingLatestSession()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);

        // Third session at 9 % passes on its own and the window average is (0 + 0 + 9) / 3 =
        // 3 %, so the speed keeps climbing.
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(18, sut.LastUsedAverageWpm);

        // Fourth session at 24 % pushes the window average to (0 + 9 + 24) / 3 = 11 %, above
        // the threshold, so the speed drops.
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_WithFailingLatestSession_SlowsDownEvenWhenWindowAverageIsGood()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(17, sut.LastUsedAverageWpm);

        // The window average is (0 + 0 + 24) / 3 = 8 %, below the 10 % threshold, so averaging
        // alone would speed up. The newest session failed at 24 %, and that veto wins.
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);
        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_KeepsOnlyTheLastWindowSizeErrorRates()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);

        // The earlier 0 % is evicted, so the 24 % session alone drives the slow-down.
        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(15, sut.LastUsedAverageWpm);

        sut.RecordSavedResult(sut.BuildResult("rx"));
        await sut.StartAsync();
        Assert.AreEqual(16, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_AdjustsAtMostOncePerSession()
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
        var result = sut.BuildResult("rx");
        sut.RecordSavedResult(result);
        // Recording the same session again must not slow down again.
        sut.RecordSavedResult(result);

        await sut.StartAsync();
        Assert.AreEqual(14, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task BuildResult_WithoutSaving_LeavesTheDynamicWpmUnchanged()
    {
        var configService = Substitute.For<IConfigurationService>();
        var config = CreateDefaultConfiguration();
        config.Practice.AutoAdjustWpm = true;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWindowSize = 1;
        configService.Current.Returns(config);

        var evaluator = Substitute.For<IPracticeResultEvaluator>();
        evaluator.Evaluate(Arg.Any<string>(), Arg.Any<string>(), 10).Returns(Failed());

        var sut = CreateController(configService, evaluator);

        // Checking the result (even repeatedly) decides nothing; only saving does.
        await sut.StartAsync();
        sut.BuildResult("rx");
        await sut.BuildResultAsync("rx");

        await sut.StartAsync();
        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(15, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public async Task RecordSavedResult_WithAutoAdjustOff_LeavesWpmAtConfigured()
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
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
        sut.RecordSavedResult(sut.BuildResult("rx"));
        Assert.IsTrue(sut.TryApplySettings(config.Clone(), out _));
        await sut.StartAsync();

        Assert.AreEqual(20, sut.LastUsedCharacterWpm);
        Assert.AreEqual(15, sut.LastUsedAverageWpm);
    }

    [TestMethod]
    public void TryApplySettings_WhenPassedTheLiveCurrent_KeepsCharacterSets()
    {
        // The backup-import path applies the reloaded live config to the controller
        // as-is; the wholesale copy inside ApplyPracticeSettings must not wipe the
        // shared CharacterSets collection while it is being re-applied to itself.
        var config = CreateDefaultConfiguration();
        config.CharacterSets["Letters"] = "ABCDE";
        config.CharacterSets["Koch-LCWO-01-KM"] = "KM";
        config.Practice.DefaultCharacterSet = "Letters";

        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(config);
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var configService = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new PracticeController(
            Substitute.For<IMorsePlayer>(),
            new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            settingsValidator,
            Substitute.For<IPracticeResultEvaluator>(),
            configService,
            CreateAdjuster(),
            Substitute.For<ILogger<PracticeController>>());

        Assert.IsTrue(sut.TryApplySettings(configService.Current, out var error));
        Assert.AreEqual(string.Empty, error);

        Assert.HasCount(3, configService.Current.CharacterSets);
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", configService.Current.CharacterSets["Default"]);
        Assert.AreEqual("ABCDE", configService.Current.CharacterSets["Letters"]);
        Assert.AreEqual("KM", configService.Current.CharacterSets["Koch-LCWO-01-KM"]);
        Assert.AreEqual("Letters", configService.Current.Practice.DefaultCharacterSet);
    }

    private static DynamicDifficultyAdjuster CreateAdjuster()
        => new(Substitute.For<ILogger<DynamicDifficultyAdjuster>>());

    private static PracticeController CreateController(
        IConfigurationService configService,
        IPracticeResultEvaluator? resultEvaluator = null,
        IPracticeSettingsValidator? settingsValidator = null)
    {
        return new PracticeController(
            Substitute.For<IMorsePlayer>(),
            new PracticeTextGeneratorFactory(Substitute.For<IMorseGenerator>()),
            settingsValidator ?? Substitute.For<IPracticeSettingsValidator>(),
            resultEvaluator ?? Substitute.For<IPracticeResultEvaluator>(),
            configService,
            CreateAdjuster(),
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
