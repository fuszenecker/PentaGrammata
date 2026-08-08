using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public async Task OpenSettingsDialogAsync_WhenApplyFails_ShowsErrorOnPracticeStatusBar()
    {
        var practiceController = CreateController();
        var settingsDialogService = Substitute.For<IMorseSettingsDialogService>();

        var snapshot = CreateConfig("Default", 5, 20, 15);
        var newSettings = CreateConfig("Custom", 8, 24, 18);

        practiceController.CreateSettingsSnapshot().Returns(snapshot);
        settingsDialogService.ShowSettingsDialogAsync(snapshot).Returns(newSettings);
        practiceController.TryApplySettings(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "Invalid settings";
                return false;
            });

        var sut = CreateSut(practiceController, settingsDialogService: settingsDialogService);

        await sut.OpenSettingsDialogAsync();

        Assert.AreEqual("Invalid settings", sut.Practice.TimeCounterText);
    }

    [TestMethod]
    public async Task OpenSettingsDialogAsync_WhenApplySucceeds_RefreshesCharacterSetsAndPracticeDuration()
    {
        var practiceController = CreateController();
        var settingsDialogService = Substitute.For<IMorseSettingsDialogService>();

        var initialSets = new List<KeyValuePair<string, string>> { new("Default", "ABCDE") };
        var updatedSets = new List<KeyValuePair<string, string>>
        {
            new("Custom", "XYZ"),
            new("Numbers", "12345"),
        };

        practiceController.PracticeDurationMins.Returns(5, 9);
        practiceController.CharacterSets.Returns(initialSets, updatedSets);
        practiceController.SelectedCharacterSet.Returns("Default", "Custom");

        var snapshot = CreateConfig("Default", 5, 20, 15);
        var newSettings = CreateConfig("Custom", 9, 24, 18);

        practiceController.CreateSettingsSnapshot().Returns(snapshot);
        settingsDialogService.ShowSettingsDialogAsync(snapshot).Returns(newSettings);
        practiceController.TryApplySettings(Arg.Any<AppConfig>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        var sut = CreateSut(practiceController, settingsDialogService: settingsDialogService);

        await sut.OpenSettingsDialogAsync();

        CollectionAssert.AreEqual(new[] { "Custom", "Numbers" }, sut.CharacterSets);
        Assert.AreEqual("Custom", sut.SelectedCharacterSet);
        Assert.AreEqual(9, sut.Practice.PracticeDuration);
    }

    [TestMethod]
    public async Task OpenAboutAsync_DelegatesToService()
    {
        var practiceController = CreateController();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        aboutDialogService.ShowAboutAsync().Returns(Task.CompletedTask);

        var sut = CreateSut(practiceController, aboutDialogService: aboutDialogService);

        await sut.OpenAboutAsync();

        await aboutDialogService.Received(1).ShowAboutAsync();
    }

    [TestMethod]
    public async Task OpenConfusionsAsync_WhenBoundListWritesStaleSelectionBack_KeepsNewlyCreatedSet()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var confusionsDialogService = Substitute.For<IConfusionsDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        var initialSets = new List<KeyValuePair<string, string>> { new("Default", "ABCDE") };
        var setsWithConfusions = new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
            new("Practice confusions", "BBB666KKK"),
        };

        // The controller is a thin facade over the configuration, so model its state:
        // the dialog switches the selection to the set it just created.
        var selected = "Default";
        var writes = new List<string?>();
        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(initialSets, setsWithConfusions);
        practiceController.SelectedCharacterSet.Returns(_ => selected);
        practiceController.When(x => x.SelectedCharacterSet = Arg.Any<string>())
            .Do(call =>
            {
                selected = call.Arg<string>();
                writes.Add(selected);
            });
        confusionsDialogService.ShowConfusionsAsync().Returns(_ =>
        {
            selected = "Practice confusions";
            return Task.CompletedTask;
        });

        var sut = CreateSut(practiceController, confusionsDialogService: confusionsDialogService, logger: logger);

        // A ComboBox bound to CharacterSets/SelectedItem reacts to a replaced ItemsSource by
        // clearing SelectedItem and then pushing its own now-stale selection back into the view
        // model (verified against Avalonia: it writes null, then the previous item). Reproduce
        // that sequence so the refresh cannot silently regress to the previously selected set.
        var staleWriteBackDone = false;
        sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainWindowViewModel.CharacterSets) || staleWriteBackDone)
            {
                return;
            }

            staleWriteBackDone = true;
            sut.SelectedCharacterSet = null!;
            sut.SelectedCharacterSet = "Default";
        };

        writes.Clear();

        await sut.OpenConfusionsAsync();

        Assert.AreEqual("Practice confusions", sut.SelectedCharacterSet);
        Assert.AreEqual("Practice confusions", practiceController.SelectedCharacterSet);
        CollectionAssert.AreEqual(new[] { "Default", "Practice confusions" }, sut.CharacterSets);

        // The stale write-back must never reach the controller, since each write there persists
        // the configuration; only the intended selection may be pushed through.
        CollectionAssert.AreEqual(new[] { "Practice confusions" }, writes);
    }

    private static IPracticeController CreateController()
    {
        var practiceController = Substitute.For<IPracticeController>();
        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        return practiceController;
    }

    private static MainWindowViewModel CreateSut(
        IPracticeController practiceController,
        IMorseSettingsDialogService? settingsDialogService = null,
        IAboutDialogService? aboutDialogService = null,
        IConfusionsDialogService? confusionsDialogService = null,
        ILogger<MainWindowViewModel>? logger = null,
        bool revealSentText = true,
        bool revealInLowercase = false)
    {
        var configService = CreateConfigService(revealSentText, revealInLowercase);
        var practice = new PracticeViewModel(
            practiceController,
            Substitute.For<IPracticeResultWindowService>(),
            configService,
            Substitute.For<ILogger<PracticeViewModel>>());
        return new MainWindowViewModel(
            practiceController,
            configService,
            settingsDialogService ?? Substitute.For<IMorseSettingsDialogService>(),
            Substitute.For<IUiSettingsDialogService>(),
            aboutDialogService ?? Substitute.For<IAboutDialogService>(),
            Substitute.For<ITrendsDialogService>(),
            confusionsDialogService ?? Substitute.For<IConfusionsDialogService>(),
            Substitute.For<IUpdateChecker>(),
            Substitute.For<IInfoDialogService>(),
            practice,
            logger ?? Substitute.For<ILogger<MainWindowViewModel>>());
    }

    private static IConfigurationService CreateConfigService(bool revealSentText = true, bool revealInLowercase = false)
    {
        var configService = Substitute.For<IConfigurationService>();
        configService.Current.Returns(new AppConfig
        {
            UiPreferences = new UiPreferences
            {
                RevealSentTextAfterPractice = revealSentText,
                RevealSentTextInLowercase = revealInLowercase,
            },
        });
        return configService;
    }

    private static AppConfig CreateConfig(string defaultSet, int duration, int charWpm, int avgWpm)
    {
        return new AppConfig
        {
            Practice = new Practice
            {
                DefaultDurationMins = duration,
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
                [defaultSet] = "ABCDE",
            },
        };
    }
}
