using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class MorseSettingsDialogViewModelTests
{
    [TestMethod]
    public void WpmUnlocked_EnablingLock_SetsAverageWpmToCharacterWpm()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var sut = new MorseSettingsDialogViewModel(CreateConfig(30, 20, "Default"), validator);

        Assert.IsFalse(sut.WpmLocked);
        Assert.AreEqual(30, sut.CharacterWpm);
        Assert.AreEqual(20, sut.AverageWpm);

        sut.WpmLocked = true;

        Assert.AreEqual(30, sut.CharacterWpm);
        Assert.AreEqual(30, sut.AverageWpm);
    }

    [TestMethod]
    public void WpmLocked_ChangingOneWpm_UpdatesTheOther()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 20, "Default"), validator);

        Assert.IsTrue(sut.WpmLocked);

        sut.CharacterWpm = 25;
        Assert.AreEqual(25, sut.AverageWpm);

        sut.AverageWpm = 22;
        Assert.AreEqual(22, sut.CharacterWpm);
    }

    [TestMethod]
    public void WpmUnlocked_CharacterWpmDrop_ClampsAverageWpm()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var sut = new MorseSettingsDialogViewModel(CreateConfig(30, 20, "Default"), validator)
        {
            AverageWpm = 28,
        };

        Assert.IsFalse(sut.WpmLocked);

        sut.CharacterWpm = 24;

        Assert.AreEqual(24, sut.AverageWpm);
    }

    [TestMethod]
    public void TryBuildSettings_WhenCharacterSetsTextInvalid_ReturnsFalseAndParserError()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            CharacterSetsText = "invalid line",
        };

        var success = sut.TryBuildSettings(out var settings);

        Assert.IsFalse(success);
        Assert.AreEqual("Character set lines must use Name = Value format.", sut.ErrorMessage);
        Assert.IsNotNull(settings);
        validator.DidNotReceive().TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>());
    }

    [TestMethod]
    public void TryBuildSettings_WhenValidationFails_ReturnsFalseAndValidationError()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "Validation failed";
                return false;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            CharacterSetsText = "Alpha = ABCDE",
        };

        var success = sut.TryBuildSettings(out _);

        Assert.IsFalse(success);
        Assert.AreEqual("Validation failed", sut.ErrorMessage);
    }

    [TestMethod]
    public void TryBuildSettings_WhenValid_ReturnsBuiltConfigWithFallbackDefaultSet()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "MissingDefault"), validator)
        {
            CharacterWpm = 28,
            AverageWpm = 17,
            SelectedSampleRate = 48000,
            Frequency = 700,
            VolumeDb = -6,
            BeepRampMs = 8,
            ErrorThreshold = 12.5,
            CharacterSetsText = "Alpha = ABCDE\nBeta = FGHIJ",
        };

        var success = sut.TryBuildSettings(out var settings);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, sut.ErrorMessage);
        Assert.AreEqual(28, settings.Practice.CharacterWpm);
        Assert.AreEqual(17, settings.Practice.AverageWpm);
        Assert.AreEqual(48000, settings.Audio.SampleRate);
        Assert.AreEqual(700, settings.Audio.Frequency);
        Assert.AreEqual(-6, settings.Audio.VolumeDb);
        Assert.AreEqual(8, settings.Audio.BeepRampMs);
        Assert.AreEqual(12.5, settings.Practice.ErrorThreshold);
        Assert.AreEqual("Alpha", settings.Practice.DefaultCharacterSet);
        Assert.HasCount(2, settings.CharacterSets);
    }

    [TestMethod]
    public void TryBuildSettings_CarriesNoiseSettingsThrough()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            SelectedNoiseType = NoiseType.Pink,
            NoiseSnrDb = 14,
            NoiseBandwidthHz = 250,
            AgcEnabled = false,
            AgcDelaySeconds = 0.6,
            ApfEnabled = false,
            ApfBandwidthHz = 100,
            ApfPeakGainDb = -2,
            CharacterSetsText = "Default = ABCDE",
        };

        var success = sut.TryBuildSettings(out var settings);

        Assert.IsTrue(success);
        Assert.AreEqual(NoiseType.Pink, settings.Audio.Noise.Type);
        // SNR of +14 dB is stored as a noise level 14 dB below the signal.
        Assert.AreEqual(-14, settings.Audio.Noise.LevelDb);
        Assert.AreEqual(250, settings.Audio.Noise.BandwidthHz);
        Assert.IsFalse(settings.Audio.Noise.AgcEnabled);
        Assert.AreEqual(0.6, settings.Audio.Noise.AgcDelaySeconds);
        Assert.IsFalse(settings.Audio.Noise.ApfEnabled);
        Assert.AreEqual(100, settings.Audio.Noise.ApfBandwidthHz);
        Assert.AreEqual(-2, settings.Audio.Noise.ApfPeakGainDb);
    }

    [TestMethod]
    public void Constructor_ReadsNoiseSettingsFromConfig()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var config = CreateConfig(20, 15, "Default");
        config.Audio.Noise = new NoiseSettings
        {
            Type = NoiseType.Uniform,
            LevelDb = -5,
            BandwidthHz = 600,
            AgcEnabled = false,
            AgcDelaySeconds = 0.25,
            ApfEnabled = false,
            ApfBandwidthHz = 150,
            ApfPeakGainDb = -1,
        };

        var sut = new MorseSettingsDialogViewModel(config, validator);

        Assert.AreEqual(NoiseType.Uniform, sut.SelectedNoiseType);
        // Config stores level -5 dB rel. signal, shown to the user as a +5 dB SNR.
        Assert.AreEqual(5, sut.NoiseSnrDb);
        Assert.AreEqual(600, sut.NoiseBandwidthHz);
        Assert.IsFalse(sut.AgcEnabled);
        Assert.AreEqual(0.25, sut.AgcDelaySeconds);
        Assert.IsFalse(sut.ApfEnabled);
        Assert.AreEqual(150, sut.ApfBandwidthHz);
        Assert.AreEqual(-1, sut.ApfPeakGainDb);
    }

    [TestMethod]
    public void Constructor_ReadsCustomTextFromConfig()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var config = CreateConfig(20, 15, "Default");
        config.Practice.CustomText = "CQ CQ DE HA5XYZ";

        var sut = new MorseSettingsDialogViewModel(config, validator);

        Assert.AreEqual("CQ CQ DE HA5XYZ", sut.CustomText);
    }

    [TestMethod]
    public void TryBuildSettings_CarriesTrimmedCustomTextThrough()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            CustomText = "  CQ DE HA5XYZ  ",
            CharacterSetsText = "Default = ABCDE",
        };

        var success = sut.TryBuildSettings(out var settings);

        Assert.IsTrue(success);
        Assert.AreEqual("CQ DE HA5XYZ", settings.Practice.CustomText);
    }

    [TestMethod]
    public void TryBuildSettings_WhenCustomTextLeftEmpty_YieldsEmptyCustomText()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            CharacterSetsText = "Default = ABCDE",
        };

        var success = sut.TryBuildSettings(out var settings);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, settings.Practice.CustomText);
    }

    [TestMethod]
    public void SaveCommand_WhenConfigValid_RaisesCloseRequestedTrue()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        validator.TryValidate(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator)
        {
            CharacterSetsText = "Default = ABCDE",
        };

        bool? closeResult = null;
        sut.CloseRequested += saved => closeResult = saved;

        sut.SaveCommand.Execute(null);

        Assert.IsTrue(closeResult);
    }

    [TestMethod]
    public void CancelCommand_RaisesCloseRequestedFalse()
    {
        var validator = Substitute.For<IPracticeSettingsValidator>();
        var sut = new MorseSettingsDialogViewModel(CreateConfig(20, 15, "Default"), validator);

        bool? closeResult = null;
        sut.CloseRequested += saved => closeResult = saved;

        sut.CancelCommand.Execute(null);

        Assert.IsFalse(closeResult);
    }

    private static AppConfig CreateConfig(int charWpm, int avgWpm, string defaultSet)
    {
        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = 5,
                CharacterWpm = charWpm,
                AverageWpm = avgWpm,
                DefaultCharacterSet = defaultSet,
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
                ["Default"] = "ABCDE",
            },
        };
    }
}
