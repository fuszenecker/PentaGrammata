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

        Assert.IsTrue(sut.Current.UiPreferences.SuppressedDialogs.Contains("ResultsSaved"));
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

    private static AppConfig CreateConfig()
    {
        return new AppConfig
        {
            Practice = new Practice { CharacterWpm = 20, AverageWpm = 15, DefaultCharacterSet = "Default" },
            CharacterSets = new CharacterSets { ["Default"] = "ABCDE" },
        };
    }
}
