using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.Configuration;
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
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.DefaultDurationMins = 2;
        config.Practice.AverageWpm = 20;
        config.Practice.CharacterWpm = 24;
        config.CharacterSets["Letters"] = "ABCDE";
        config.Practice.DefaultCharacterSet = "Letters";
        configStore.Load().Returns(config);

        morseGenerator.GenerateGroupsOf5("ABCDE", 28).Returns("ABCDE FGHIJ");

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configStore, logger);

        await sut.StartAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.LastGeneratedText);
        Assert.IsFalse(sut.IsPracticing);
        morseGenerator.Received(1).GenerateGroupsOf5("ABCDE", 28);
        await morsePlayer.Received(1).PlayMorseCodeAsync(
            "vvv = ABCDE FGHIJ <ar>",
            24,
            20,
            config.Audio.SampleRate,
            config.Audio.Frequency,
            config.Audio.Volume,
            config.Audio.BeepRampMs,
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public void BuildResult_DelegatesToEvaluator_AndStoresLastReceivedText()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.Practice.ErrorThreshold = 17.5;
        configStore.Load().Returns(config);

        var expected = new PracticeResult
        {
            CharacterCount = 5,
            ErrorCount = 1,
            ErrorRatePercent = 20,
            IsSuccessful = false,
        };
        resultEvaluator.Evaluate(string.Empty, "RX", 17.5).Returns(expected);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configStore, logger);

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
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        configStore.Load().Returns(CreateDefaultConfiguration());
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "invalid settings";
                return false;
            });

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configStore, logger);

        var success = sut.TryApplySettings(CreateDefaultConfiguration(), out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("invalid settings", error);
        configStore.DidNotReceive().SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public void TryApplySettings_WhenValid_UpdatesStateAndPersistsSnapshot()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var loadedConfig = CreateDefaultConfiguration();
        configStore.Load().Returns(loadedConfig);
        configStore.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        settingsValidator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configStore, logger);
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
                Volume = 0.3,
                BeepRampMs = 6,
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

        configStore.Received(1).SaveAsync(Arg.Is<AppConfig>(saved =>
            saved.Practice.DefaultDurationMins == 7
            && saved.Practice.CharacterWpm == 30
            && saved.Practice.AverageWpm == 18
            && saved.Practice.DefaultCharacterSet == "Custom"
            && saved.Practice.ErrorThreshold == 12.5
            && saved.Audio.SampleRate == 48000
            && saved.Audio.Frequency == 700
            && saved.Audio.Volume == 0.3
            && saved.Audio.BeepRampMs == 6
            && saved.CharacterSets.Count == 1
            && saved.CharacterSets["Custom"] == "ABCDE"));
    }

    [TestMethod]
    public void Constructor_WhenCharacterSetsAreEmpty_AddsDefaultSet()
    {
        var morsePlayer = Substitute.For<IMorsePlayer>();
        var morseGenerator = Substitute.For<IMorseGenerator>();
        var settingsValidator = Substitute.For<IPracticeSettingsValidator>();
        var resultEvaluator = Substitute.For<IPracticeResultEvaluator>();
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var logger = Substitute.For<ILogger<PracticeController>>();

        var config = CreateDefaultConfiguration();
        config.CharacterSets.Clear();
        config.Practice.DefaultCharacterSet = null!;
        configStore.Load().Returns(config);

        var sut = new PracticeController(morsePlayer, morseGenerator, settingsValidator, resultEvaluator, configStore, logger);

        Assert.AreEqual(1, sut.CharacterSets.Count);
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
                Volume = 0.7,
                BeepRampMs = 4,
            },
            CharacterSets = new CharacterSets
            {
                ["Default"] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            },
        };
    }
}
