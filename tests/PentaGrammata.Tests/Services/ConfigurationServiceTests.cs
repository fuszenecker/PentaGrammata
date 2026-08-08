using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class ConfigurationServiceTests
{
    [TestMethod]
    public void Current_ReturnsConfigurationLoadedFromStore()
    {
        var store = Substitute.For<IConfigurationStore>();
        var loaded = CreateConfig();
        store.Load().Returns(loaded);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        Assert.AreSame(loaded, sut.Current);
        store.Received(1).Load();
    }

    [TestMethod]
    public async Task SaveAsync_PersistsAnIsolatedSnapshot_NotTheLiveInstance()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        AppConfig? persisted = null;
        store.SaveAsync(Arg.Do<AppConfig>(c => persisted = c)).Returns(Task.CompletedTask);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        await sut.SaveAsync();

        Assert.IsNotNull(persisted);
        // A snapshot, not the live Current, so later mutations can't affect the write.
        Assert.AreNotSame(sut.Current, persisted);
        Assert.AreEqual(sut.Current.Practice.CharacterWpm, persisted!.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task SaveAsync_SnapshotReflectsStateAtCallTime_NotLaterMutations()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        AppConfig? persisted = null;
        store.SaveAsync(Arg.Do<AppConfig>(c => persisted = c)).Returns(Task.CompletedTask);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());
        sut.Current.Practice.CharacterWpm = 33;

        var saveTask = sut.SaveAsync();
        // Mutate after the snapshot was taken; the persisted value must be the old one.
        sut.Current.Practice.CharacterWpm = 99;
        await saveTask;

        Assert.AreEqual(33, persisted!.Practice.CharacterWpm);
    }

    [TestMethod]
    public async Task SaveAsync_WhenStoreThrows_DoesNotPropagate()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.FromException(new System.IO.IOException("disk full")));

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        // Should be swallowed (logged), not thrown to the caller.
        await sut.SaveAsync();
    }

    [TestMethod]
    public void IsDialogSuppressed_TrueWhenKeyPresent_FalseOtherwise()
    {
        var store = Substitute.For<IConfigurationStore>();
        var config = CreateConfig();
        config.UiPreferences.SuppressedDialogs.Add("ResultsSaved");
        store.Load().Returns(config);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        Assert.IsTrue(sut.IsDialogSuppressed("ResultsSaved"));
        Assert.IsFalse(sut.IsDialogSuppressed("Other"));
        Assert.IsFalse(sut.IsDialogSuppressed(string.Empty));
    }

    [TestMethod]
    public async Task SuppressDialogAsync_AddsKeyToCurrentAndPersists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        await sut.SuppressDialogAsync("ResultsSaved");

        Assert.Contains("ResultsSaved", sut.Current.UiPreferences.SuppressedDialogs);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public async Task SuppressDialogAsync_WhenAlreadySuppressed_DoesNotDuplicateOrSave()
    {
        var store = Substitute.For<IConfigurationStore>();
        var config = CreateConfig();
        config.UiPreferences.SuppressedDialogs.Add("ResultsSaved");
        store.Load().Returns(config);

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        await sut.SuppressDialogAsync("ResultsSaved");

        Assert.HasCount(1, sut.Current.UiPreferences.SuppressedDialogs);
        await store.DidNotReceive().SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public void Constructor_WhenCharacterSetsAreEmpty_AddsDefaultSetAndSelectsIt()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(new AppConfig
        {
            Practice = new Practice { DefaultCharacterSet = null! },
            CharacterSets = new CharacterSets(),
        });

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        Assert.HasCount(1, sut.Current.CharacterSets);
        Assert.IsTrue(sut.Current.CharacterSets.ContainsKey("Default"));
        Assert.AreEqual("Default", sut.Current.Practice.DefaultCharacterSet);
    }

    [TestMethod]
    public void Constructor_WhenDefaultCharacterSetIsBlank_SelectsFirstAvailableSet()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(new AppConfig
        {
            Practice = new Practice { DefaultCharacterSet = "   " },
            CharacterSets = new CharacterSets { ["Letters"] = "ABCDE", ["Numbers"] = "12345" },
        });

        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        Assert.AreEqual("Letters", sut.Current.Practice.DefaultCharacterSet);
    }

    [TestMethod]
    public async Task SetPracticeDuration_WhenChanged_PersistsAndIsNoOpWhenUnchanged()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        sut.SetPracticeDuration(42);
        await sut.FlushAsync();

        Assert.AreEqual(42, sut.Current.Practice.DefaultDurationMins);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());

        sut.SetPracticeDuration(42);
        await sut.FlushAsync();
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>()); // still only one save
    }

    [TestMethod]
    public async Task SetSelectedCharacterSet_WhenChanged_Persists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        sut.SetSelectedCharacterSet("Other");
        await sut.FlushAsync();

        Assert.AreEqual("Other", sut.Current.Practice.DefaultCharacterSet);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public async Task ApplyPracticeSettings_CopiesAllFieldsDropsBlankSetsAndPersists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        var newSettings = new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = 7,
                CharacterWpm = 30,
                AverageWpm = 18,
                DefaultCharacterSet = "Custom",
                ErrorThreshold = 12.5,
                CustomText = "CQ DE",
                AutoAdjustWpm = true,
                AutoAdjustWindowSize = 5,
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

        sut.ApplyPracticeSettings(newSettings);
        await sut.FlushAsync();

        Assert.AreEqual(7, sut.Current.Practice.DefaultDurationMins);
        Assert.AreEqual(30, sut.Current.Practice.CharacterWpm);
        Assert.AreEqual(18, sut.Current.Practice.AverageWpm);
        Assert.AreEqual("Custom", sut.Current.Practice.DefaultCharacterSet);
        Assert.AreEqual(12.5, sut.Current.Practice.ErrorThreshold);
        Assert.AreEqual("CQ DE", sut.Current.Practice.CustomText);
        Assert.IsTrue(sut.Current.Practice.AutoAdjustWpm);
        Assert.AreEqual(5, sut.Current.Practice.AutoAdjustWindowSize);
        Assert.AreEqual(48000, sut.Current.Audio.SampleRate);
        Assert.AreEqual(700, sut.Current.Audio.Frequency);
        Assert.AreEqual(-10, sut.Current.Audio.VolumeDb);
        Assert.AreEqual(6, sut.Current.Audio.BeepRampMs);
        Assert.AreEqual(NoiseType.Uniform, sut.Current.Audio.Noise.Type);
        Assert.AreEqual(-8, sut.Current.Audio.Noise.LevelDb);
        Assert.AreEqual(350, sut.Current.Audio.Noise.BandwidthHz);
        Assert.IsFalse(sut.Current.Audio.Noise.AgcEnabled);
        Assert.AreEqual(0.9, sut.Current.Audio.Noise.AgcDelaySeconds);
        Assert.IsFalse(sut.Current.Audio.Noise.ApfEnabled);
        Assert.AreEqual(70, sut.Current.Audio.Noise.ApfBandwidthHz);
        Assert.AreEqual(-4, sut.Current.Audio.Noise.ApfPeakGainDb);
        Assert.HasCount(1, sut.Current.CharacterSets);
        Assert.AreEqual("ABCDE", sut.Current.CharacterSets["Custom"]);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public async Task ApplyUiPreferencesAsync_ReplacesPreferencesAndPersists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        var newPrefs = new UiPreferences
        {
            ReceivedTextFontFamily = "Comic Sans",
            ReceivedTextFontSize = 42,
            RevealSentTextInLowercase = true,
        };

        await sut.ApplyUiPreferencesAsync(newPrefs);

        Assert.AreEqual("Comic Sans", sut.Current.UiPreferences.ReceivedTextFontFamily);
        Assert.AreEqual(42, sut.Current.UiPreferences.ReceivedTextFontSize);
        Assert.IsTrue(sut.Current.UiPreferences.RevealSentTextInLowercase);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
        // The persisted graph must be an independent copy, not the caller's instance.
        Assert.AreNotSame(newPrefs, sut.Current.UiPreferences);
    }

    [TestMethod]
    public async Task SetConfusionsHalfLife_WhenChanged_Persists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        sut.SetConfusionsHalfLife(14);
        await sut.FlushAsync();

        Assert.AreEqual(14, sut.Current.Analytics.ConfusionsHalfLifeDays);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
    }

    [TestMethod]
    public async Task UpsertCharacterSetAndSelectAsync_AddsSetSelectsItAndPersists()
    {
        var store = Substitute.For<IConfigurationStore>();
        store.Load().Returns(CreateConfig());
        store.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
        var sut = new ConfigurationService(store, Substitute.For<ILogger<ConfigurationService>>());

        await sut.UpsertCharacterSetAndSelectAsync("Practice confusions", "AABBCC");

        Assert.AreEqual("AABBCC", sut.Current.CharacterSets["Practice confusions"]);
        Assert.AreEqual("Practice confusions", sut.Current.Practice.DefaultCharacterSet);
        await store.Received(1).SaveAsync(Arg.Any<AppConfig>());
    }

    private static AppConfig CreateConfig()
    {
        return new AppConfig
        {
            Practice = new Practice { CharacterWpm = 20, AverageWpm = 15, DefaultCharacterSet = "Default" },
            CharacterSets = new CharacterSets { ["Default"] = "ABCDE" },
        };
    }
}
