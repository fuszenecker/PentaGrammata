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
