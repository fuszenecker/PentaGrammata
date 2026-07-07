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
        Assert.AreEqual(original.Audio.SampleRate, clone.Audio.SampleRate);
        Assert.AreEqual(original.Audio.Frequency, clone.Audio.Frequency);
        Assert.AreEqual(original.Audio.Volume, clone.Audio.Volume);
        Assert.AreEqual(original.Audio.BeepRampMs, clone.Audio.BeepRampMs);
        Assert.AreEqual(original.UiPreferences.ReceivedTextFontSize, clone.UiPreferences.ReceivedTextFontSize);
        Assert.AreEqual(original.UiPreferences.RevealSentTextAfterPractice, clone.UiPreferences.RevealSentTextAfterPractice);
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
        Assert.AreNotSame(original.Audio, clone.Audio);
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

        Assert.HasCount(1, original.CharacterSets);
        Assert.AreEqual("ABCDE", original.CharacterSets["Default"]);
        CollectionAssert.AreEqual(new[] { "ResultsSaved" }, original.UiPreferences.SuppressedDialogs);
        Assert.AreEqual(20, original.Practice.CharacterWpm);
    }

    [TestMethod]
    public void Clone_IncludesUiPreferences()
    {
        // Regression guard: an earlier hand-written snapshot dropped UiPreferences.
        var original = CreateSample();
        original.UiPreferences.ReceivedTextFontSize = 42.0;
        original.UiPreferences.RevealSentTextAfterPractice = false;

        var clone = original.Clone();

        Assert.AreEqual(42.0, clone.UiPreferences.ReceivedTextFontSize);
        Assert.IsFalse(clone.UiPreferences.RevealSentTextAfterPractice);
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
            UiPreferences = new UiPreferences
            {
                SuppressedDialogs = { "ResultsSaved" },
                ReceivedTextFontSize = 24.0,
                RevealSentTextAfterPractice = true,
            },
        };
    }
}
