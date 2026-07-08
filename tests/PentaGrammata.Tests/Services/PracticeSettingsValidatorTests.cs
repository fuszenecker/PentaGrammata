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
    public void TryValidate_VolumeOutOfRange_ReturnsFalse()
    {
        var settings = CreateValidSettings();
        settings.Audio.Volume = 1.01;

        var success = _validator.TryValidate(settings, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual("Volume must be between 0 and 1.", error);
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
                Volume = 0.7,
                BeepRampMs = 4,
            },
            CharacterSets = new CharacterSets
            {
                ["Default"] = "ABCDE",
            },
        };
    }
}
