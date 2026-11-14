using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeSettingsValidatorTests
{
    private readonly PracticeSettingsValidator _validator = new();

    [TestMethod]
    public void TryValidate_ValidSettings_ReturnsTrue()
    {
        var settings = CreateValidSettings();

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_DefaultDurationBelowOne_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.DefaultDurationMins = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Default duration must be at least 1 minute.", error);
    }

    [TestMethod]
    public void TryValidate_AverageWpmGreaterThanCharacterWpm_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.CharacterWpm = 18;
        settings.Practice.AverageWpm = 19;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Average WPM cannot exceed character WPM.", error);
    }

    [TestMethod]
    public void TryValidate_VolumeAboveFullScale_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.VolumeDb = 0.5;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Volume must be 0 dBFS or lower.", error);
    }

    [TestMethod]
    public void TryValidate_NoiseEnabledWithNonPositiveBandwidth_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.BandwidthHz = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Noise filter width must be greater than 0.", error);
    }

    [TestMethod]
    public void TryValidate_AgcEnabledWithNonPositiveDelay_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.AgcEnabled = true;
        settings.Audio.Noise.AgcDelaySeconds = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("AGC delay must be greater than 0.", error);
    }

    [TestMethod]
    public void TryValidate_AgcEnabledWithNegativeMaxGainDb_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.AgcEnabled = true;
        settings.Audio.Noise.AgcMaxGainDb = -1;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("AGC max gain must be 0 dB or higher.", error);
    }

    [TestMethod]
    public void TryValidate_ApfEnabledWithNonPositiveBandwidth_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.ApfEnabled = true;
        settings.Audio.Noise.ApfBandwidthHz = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("APF bandwidth must be greater than 0.", error);
    }

    [TestMethod]
    public void TryValidate_ApfEnabledWithNegativePeakGainDb_ReturnsTrue()
    {
        // Peak amplification is now in decibels; negative values simply attenuate the
        // blended peak and are perfectly valid.
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.ApfEnabled = true;
        settings.Audio.Noise.ApfPeakGainDb = -20;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_DisabledStagesWithBadValues_ReturnsTrue()
    {
        // AGC/APF params are only validated when their stage is enabled.
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.Gaussian;
        settings.Audio.Noise.AgcEnabled = false;
        settings.Audio.Noise.AgcDelaySeconds = 0;
        settings.Audio.Noise.AgcMaxGainDb = -1;
        settings.Audio.Noise.ApfEnabled = false;
        settings.Audio.Noise.ApfBandwidthHz = 0;
        settings.Audio.Noise.ApfPeakGainDb = -5;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_NoiseNoneWithZeroBandwidth_ReturnsTrue()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.None;
        settings.Audio.Noise.BandwidthHz = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_QsbEnabledWithNonPositiveDepth_ReturnsFalse()
    {
        // QSB is a property of the signal, not the receiver chain, so it must be
        // validated even with the noise type set to None.
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.None;
        settings.Audio.Noise.QsbEnabled = true;
        settings.Audio.Noise.QsbDepthDb = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("QSB fade depth must be greater than 0.", error);
    }

    [TestMethod]
    public void TryValidate_QsbEnabledWithNonPositivePeriod_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.None;
        settings.Audio.Noise.QsbEnabled = true;
        settings.Audio.Noise.QsbPeriodSeconds = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("QSB fade period must be greater than 0.", error);
    }

    [TestMethod]
    public void TryValidate_QsbEnabledWithNoiseNone_ReturnsTrue()
    {
        // Fading with a clean signal is a valid combination: QSB works independently
        // of the noise chain.
        var settings = CreateValidSettings();
        settings.Audio.Noise.Type = NoiseType.None;
        settings.Audio.Noise.QsbEnabled = true;
        settings.Audio.Noise.QsbDepthDb = 10;
        settings.Audio.Noise.QsbPeriodSeconds = 5;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_QsbDisabledWithBadValues_ReturnsTrue()
    {
        // QSB params are only validated when the stage is enabled.
        var settings = CreateValidSettings();
        settings.Audio.Noise.QsbEnabled = false;
        settings.Audio.Noise.QsbDepthDb = 0;
        settings.Audio.Noise.QsbPeriodSeconds = 0;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_DefaultCharacterSetNotConfigured_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.DefaultCharacterSet = "Missing";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Default character set must match one of the configured character set names.", error);
    }

    [TestMethod]
    public void TryValidate_EmptyCustomText_ReturnsTrue()
    {
        // Empty means "generate groups as usual", so it must not be validated as sendable text.
        var settings = CreateValidSettings();
        settings.Practice.CustomText = "   ";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_SendableCustomText_ReturnsTrue()
    {
        var settings = CreateValidSettings();
        settings.Practice.CustomText = "cq cq de ha5xyz <ar>";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_CustomTextWithUnsendableCharacter_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.CustomText = "HELLO! WORLD";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Custom text contains something that cannot be sent as Morse code: \"!\".", error);
    }

    [TestMethod]
    public void TryValidate_CustomTextWithUnknownProsign_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.CustomText = "CQ <zz> DE";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Custom text contains something that cannot be sent as Morse code: \"<zz>\".", error);
    }

    [TestMethod]
    public void TryValidate_MultiLineCustomText_ReturnsTrue()
    {
        // Line breaks normalize to word gaps, so they must not be reported as unsendable.
        var settings = CreateValidSettings();
        settings.Practice.CustomText = "CQ CQ\r\nDE HA5XYZ\nK";

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryValidate_CustomTextTopicWithoutText_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = PracticeTopic.CustomText;
        settings.Practice.CustomText = "  ";

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual("The custom text topic needs a custom text.", error);
    }

    [TestMethod]
    public void TryValidate_BlankCustomTextWithAnotherTopic_ReturnsTrue()
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = PracticeTopic.Groups;
        settings.Practice.CustomText = string.Empty;

        Assert.IsTrue(_validator.TryValidate(settings, out _));
    }

    [TestMethod]
    [DataRow(PracticeTopic.CallsignsEmea, "EMEA callsign patterns")]
    [DataRow(PracticeTopic.CallsignsGlobal, "global callsign patterns")]
    [DataRow(PracticeTopic.Cities, "cities")]
    [DataRow(PracticeTopic.Names, "names")]
    [DataRow(PracticeTopic.Abbreviations, "abbreviations")]
    public void TryValidate_SelectedTopicWithEmptyList_ReturnsFalse(PracticeTopic topic, string listName)
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = topic;

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual($"The selected topic needs at least one entry in the list of {listName}.", error);
    }

    [TestMethod]
    public void TryValidate_EmptyListsOfOtherTopics_AreAllowed()
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = PracticeTopic.Mixed;

        Assert.IsTrue(_validator.TryValidate(settings, out _));
    }

    [TestMethod]
    [DataRow("HA#xyz")]
    [DataRow("DL/HA#x")]
    [DataRow("9A1ABC")]
    public void TryValidate_SendableCallsignPatterns_ReturnTrue(string pattern)
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = PracticeTopic.CallsignsEmea;
        settings.Topics.CallsignPatternsEmea = [pattern];

        Assert.IsTrue(_validator.TryValidate(settings, out var error), error);
    }

    [TestMethod]
    public void TryValidate_CallsignPatternWithUnsendableCharacter_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Topics.CallsignPatternsGlobal = ["W#x$"];

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual("Global callsign pattern \"W#x$\" contains something that cannot be sent as Morse code: \"$\".", error);
    }

    [TestMethod]
    public void TryValidate_CallsignPatternWithSpace_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Topics.CallsignPatternsEmea = ["HA# xyz"];

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual("EMEA callsign pattern \"HA# xyz\" must not contain spaces.", error);
    }

    [TestMethod]
    [DataRow("City", "MÜNCHEN", "Ü")]
    [DataRow("Name", "JOSÉ", "É")]
    [DataRow("Abbreviation", "QRZ?!", "!")]
    public void TryValidate_UnsendableWord_ReturnsFalse(string label, string word, string token)
    {
        var settings = CreateValidSettings();
        switch (label)
        {
            case "City": settings.Topics.Cities = ["WIEN", word]; break;
            case "Name": settings.Topics.Names = [word]; break;
            default: settings.Topics.Abbreviations = [word]; break;
        }

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual($"{label} \"{word}\" contains something that cannot be sent as Morse code: \"{token}\".", error);
    }

    [TestMethod]
    public void TryValidate_MultiWordEntries_AreAllowed()
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = PracticeTopic.Cities;
        settings.Topics.Cities = ["Cape Town", "Tel Aviv"];

        Assert.IsTrue(_validator.TryValidate(settings, out var error), error);
    }

    [TestMethod]
    public void TryValidate_UndefinedTopic_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Practice.Topic = (PracticeTopic)99;

        Assert.IsFalse(_validator.TryValidate(settings, out var error));
        Assert.AreEqual("Unknown practice topic.", error);
    }

    private static AppConfig CreateValidSettings()
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
                ["Default"] = "ABCDE",
            },
        };
    }
}
