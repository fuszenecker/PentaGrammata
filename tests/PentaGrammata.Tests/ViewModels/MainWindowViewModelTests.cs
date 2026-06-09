using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class MainWindowViewModelTests
{
    [TestMethod]
    public async Task StartPracticeAsync_DelegatesToController_AndUnlocksResultCheckAfterInput()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        practiceController.StartAsync().Returns(Task.CompletedTask);

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger);
        sut.ReceivedText = "TO_BE_CLEARED";

        await sut.StartPracticeAsync();

        await practiceController.Received(1).StartAsync();
        Assert.IsFalse(sut.IsPracticeRunning);
        Assert.AreEqual(string.Empty, sut.ReceivedText);
        Assert.IsTrue(sut.StartPracticeCommand.CanExecute(null));
        Assert.IsFalse(sut.StopPracticeCommand.CanExecute(null));
        Assert.IsFalse(sut.CheckResultCommand.CanExecute(null));

        sut.ReceivedText = "RX";
        Assert.IsTrue(sut.CheckResultCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenControllerThrows_ShowsFailureMessage()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        practiceController.StartAsync().Returns(_ => throw new InvalidOperationException("boom"));

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger);

        await sut.StartPracticeAsync();

        Assert.AreEqual("Practice failed. Check logs for details.", sut.TimeCounterText);
        Assert.IsFalse(sut.IsPracticeRunning);
    }

    [TestMethod]
    public async Task OpenSettingsDialogAsync_WhenApplyFails_ShowsError()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");

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

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger);

        await sut.OpenSettingsDialogAsync();

        Assert.AreEqual("Invalid settings", sut.TimeCounterText);
    }

    [TestMethod]
    public async Task OpenSettingsDialogAsync_WhenApplySucceeds_RefreshesLocalProperties()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

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

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger);

        await sut.OpenSettingsDialogAsync();

        CollectionAssert.AreEqual(new[] { "Custom", "Numbers" }, sut.CharacterSets);
        Assert.AreEqual("Custom", sut.SelectedCharacterSet);
        Assert.AreEqual(9, sut.PracticeDuration);
    }

    [TestMethod]
    public async Task OpenResultWindowAsync_BuildsAndShowsPracticeResult()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        practiceController.IsResultSaved.Returns(false);

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, false).Returns(Task.FromResult(false));

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger)
        {
            ReceivedText = "RX",
        };

        await sut.OpenResultWindowAsync();

        practiceController.Received(1).BuildResult("RX");
        await resultWindowService.Received(1).ShowPracticeResultAsync(result, 20, 15, false);
    }

    [TestMethod]
    public async Task OpenResultWindowAsync_WhenSaved_SetsIsResultSavedOnController()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        practiceController.IsResultSaved.Returns(false);

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, false).Returns(Task.FromResult(true));

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger)
        {
            ReceivedText = "RX",
        };

        await sut.OpenResultWindowAsync();

        practiceController.IsResultSaved = true;
    }

    [TestMethod]
    public async Task OpenResultWindowAsync_PassesAlreadySavedFlag()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        practiceController.IsResultSaved.Returns(true);

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, true).Returns(Task.FromResult(false));

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger)
        {
            ReceivedText = "RX",
        };

        await sut.OpenResultWindowAsync();

        await resultWindowService.Received(1).ShowPracticeResultAsync(result, 20, 15, true);
    }

    [TestMethod]
    public async Task OpenAboutAsync_DelegatesToService()
    {
        var practiceController = Substitute.For<IPracticeController>();
        var settingsDialogService = Substitute.For<ISettingsDialogService>();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        var aboutDialogService = Substitute.For<IAboutDialogService>();
        var logger = Substitute.For<ILogger<MainWindowViewModel>>();

        practiceController.PracticeDurationMins.Returns(5);
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Default", "ABCDE"),
        });
        practiceController.SelectedCharacterSet.Returns("Default");
        aboutDialogService.ShowAboutAsync().Returns(Task.CompletedTask);

        var sut = new MainWindowViewModel(practiceController, settingsDialogService, resultWindowService, aboutDialogService, logger);

        await sut.OpenAboutAsync();

        await aboutDialogService.Received(1).ShowAboutAsync();
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
                Volume = 0.7,
                BeepRampMs = 4,
            },
            CharacterSets = new CharacterSets
            {
                [defaultSet] = "ABCDE",
            },
        };
    }
}
