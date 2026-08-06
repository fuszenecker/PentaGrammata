using PentaGrammata.Configuration;

namespace PentaGrammata.Tests.Configuration;

[TestClass]
public sealed class AppConfigurationCloneTests
{
    [TestMethod]
    public void Clone_CopiesAllScalarValues()
    {
        var original = CreateSample();

        var clone = original.Clone();

        Assert.AreEqual(original.Practice.DefaultDurationMins, clone.Practice.DefaultDurationMins);
        Assert.AreEqual(original.Practice.CharacterWpm, clone.Practice.CharacterWpm);
        Assert.AreEqual(original.Practice.AverageWpm, clone.Practice.AverageWpm);
        Assert.AreEqual(original.Practice.DefaultCharacterSet, clone.Practice.DefaultCharacterSet);
        Assert.AreEqual(original.Practice.ErrorThreshold, clone.Practice.ErrorThreshold);
        Assert.AreEqual(original.Practice.CustomText, clone.Practice.CustomText);
        Assert.AreEqual(original.Practice.AutoAdjustWpm, clone.Practice.AutoAdjustWpm);
        Assert.AreEqual(original.Practice.AutoAdjustWindowSize, clone.Practice.AutoAdjustWindowSize);
        Assert.AreEqual(original.Analytics.ConfusionsHalfLifeDays, clone.Analytics.ConfusionsHalfLifeDays);
        Assert.AreEqual(original.Audio.SampleRate, clone.Audio.SampleRate);
        Assert.AreEqual(original.Audio.Frequency, clone.Audio.Frequency);
        Assert.AreEqual(original.Audio.VolumeDb, clone.Audio.VolumeDb);
        Assert.AreEqual(original.Audio.BeepRampMs, clone.Audio.BeepRampMs);
        Assert.AreEqual(original.Audio.Noise.Type, clone.Audio.Noise.Type);
        Assert.AreEqual(original.Audio.Noise.LevelDb, clone.Audio.Noise.LevelDb);
        Assert.AreEqual(original.Audio.Noise.BandwidthHz, clone.Audio.Noise.BandwidthHz);
        Assert.AreEqual(original.Audio.Noise.AgcEnabled, clone.Audio.Noise.AgcEnabled);
        Assert.AreEqual(original.Audio.Noise.AgcDelaySeconds, clone.Audio.Noise.AgcDelaySeconds);
        Assert.AreEqual(original.Audio.Noise.ApfEnabled, clone.Audio.Noise.ApfEnabled);
        Assert.AreEqual(original.Audio.Noise.ApfBandwidthHz, clone.Audio.Noise.ApfBandwidthHz);
        Assert.AreEqual(original.Audio.Noise.ApfPeakGainDb, clone.Audio.Noise.ApfPeakGainDb);
        Assert.AreEqual(original.UiPreferences.ReceivedTextFontFamily, clone.UiPreferences.ReceivedTextFontFamily);
        Assert.AreEqual(original.UiPreferences.ReceivedTextFontSize, clone.UiPreferences.ReceivedTextFontSize);
        Assert.AreEqual(original.UiPreferences.RevealSentTextAfterPractice, clone.UiPreferences.RevealSentTextAfterPractice);
        Assert.AreEqual(original.UiPreferences.RevealSentTextInLowercase, clone.UiPreferences.RevealSentTextInLowercase);
        CollectionAssert.AreEqual(original.UiPreferences.SuppressedDialogs, clone.UiPreferences.SuppressedDialogs);
        CollectionAssert.AreEqual(
            new[] { "Default" },
            new List<string>(clone.CharacterSets.Keys));
        Assert.AreEqual("ABCDE", clone.CharacterSets["Default"]);
    }

    [TestMethod]
    public void Clone_ProducesIndependentNestedInstances()
    {
        var original = CreateSample();

        var clone = original.Clone();

        Assert.AreNotSame(original.Practice, clone.Practice);
        Assert.AreNotSame(original.Analytics, clone.Analytics);
        Assert.AreNotSame(original.Audio, clone.Audio);
        Assert.AreNotSame(original.Audio.Noise, clone.Audio.Noise);
        Assert.AreNotSame(original.UiPreferences, clone.UiPreferences);
        Assert.AreNotSame(original.CharacterSets, clone.CharacterSets);
        Assert.AreNotSame(original.UiPreferences.SuppressedDialogs, clone.UiPreferences.SuppressedDialogs);
    }

    [TestMethod]
    public void Clone_MutatingCloneDoesNotAffectOriginalCollections()
    {
        var original = CreateSample();
        var clone = original.Clone();

        clone.CharacterSets["Extra"] = "XYZ";
        clone.CharacterSets["Default"] = "CHANGED";
        clone.UiPreferences.SuppressedDialogs.Add("AnotherDialog");
        clone.Practice.CharacterWpm = 99;
        clone.Analytics.ConfusionsHalfLifeDays = 99;

        Assert.HasCount(1, original.CharacterSets);
        Assert.AreEqual("ABCDE", original.CharacterSets["Default"]);
        CollectionAssert.AreEqual(new[] { "ResultsSaved" }, original.UiPreferences.SuppressedDialogs);
        Assert.AreEqual(20, original.Practice.CharacterWpm);
        Assert.AreEqual(7, original.Analytics.ConfusionsHalfLifeDays);
    }

    [TestMethod]
    public void Clone_IncludesUiPreferences()
    {
        // Regression guard: an earlier hand-written snapshot dropped UiPreferences.
        var original = CreateSample();
        original.UiPreferences.ReceivedTextFontFamily = "Ubuntu Mono";
        original.UiPreferences.ReceivedTextFontSize = 42.0;
        original.UiPreferences.RevealSentTextAfterPractice = false;
        original.UiPreferences.RevealSentTextInLowercase = false;

        var clone = original.Clone();

        Assert.AreEqual("Ubuntu Mono", clone.UiPreferences.ReceivedTextFontFamily);
        Assert.AreEqual(42.0, clone.UiPreferences.ReceivedTextFontSize);
        Assert.IsFalse(clone.UiPreferences.RevealSentTextAfterPractice);
        Assert.IsFalse(clone.UiPreferences.RevealSentTextInLowercase);
    }

    private static AppConfiguration CreateSample()
    {
        return new AppConfiguration
        {
            Practice = new Practice
            {
                DefaultDurationMins = 5,
                CharacterWpm = 20,
                AverageWpm = 15,
                DefaultCharacterSet = "Default",
                ErrorThreshold = 10,
                CustomText = "CQ CQ DE HA5XYZ",
                AutoAdjustWpm = true,
                AutoAdjustWindowSize = 7,
            },
            Analytics = new Analytics
            {
                ConfusionsHalfLifeDays = 7,
            },
            Audio = new Audio
            {
                SampleRate = 44100,
                Frequency = 523.25,
                VolumeDb = -3,
                BeepRampMs = 4,
                Noise = new NoiseSettings
                {
                    Type = NoiseType.Pink,
                    LevelDb = -12.5,
                    BandwidthHz = 400,
                    AgcEnabled = false,
                    AgcDelaySeconds = 0.8,
                    ApfEnabled = false,
                    ApfBandwidthHz = 90,
                    ApfPeakGainDb = -6,
                },
            },
            CharacterSets = new CharacterSets
            {
                ["Default"] = "ABCDE",
            },
            UiPreferences = new UiPreferences
            {
                SuppressedDialogs = { "ResultsSaved" },
                ReceivedTextFontFamily = "Cascadia Mono",
                ReceivedTextFontSize = 20.0,
                RevealSentTextAfterPractice = true,
                RevealSentTextInLowercase = true,
            },
        };
    }
}
