using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class PracticeViewModelTests
{
    [TestMethod]
    public async Task StartPracticeAsync_DelegatesToController_AndUnlocksResultCheckAfterInput()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns(string.Empty);

        var sut = CreateSut(practiceController);
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
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(_ => throw new InvalidOperationException("boom"));

        var sut = CreateSut(practiceController);

        await sut.StartPracticeAsync();

        Assert.AreEqual("Practice failed. Check logs for details.", sut.TimeCounterText);
        Assert.IsFalse(sut.IsPracticeRunning);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenRevealEnabledAndReceivedTextEmpty_FillsWithGeneratedText()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        var sut = CreateSut(practiceController, revealSentText: true);

        await sut.StartPracticeAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenRevealDisabledAndReceivedTextEmpty_DoesNotFill()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        var sut = CreateSut(practiceController, revealSentText: false);

        await sut.StartPracticeAsync();

        Assert.AreEqual(string.Empty, sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenRevealEnabledAndReceivedTextNotEmpty_DoesNotOverwriteWithGeneratedText()
    {
        var practiceController = CreateController();
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        // Simulate the user typing during practice by setting ReceivedText after StartAsync is awaited.
        practiceController.StartAsync().Returns(async _ =>
        {
            await Task.Yield();
        });

        var sut = CreateSut(practiceController, revealSentText: true);

        var startTask = sut.StartPracticeAsync();
        // At this point StartAsync has yielded; set ReceivedText to simulate user input.
        sut.ReceivedText = "MY COPY";
        await startTask;

        Assert.AreEqual("MY COPY", sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenLowercaseRevealEnabled_FillsWithLowercasedGeneratedText()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        var sut = CreateSut(practiceController, revealSentText: true, revealInLowercase: true);

        await sut.StartPracticeAsync();

        Assert.AreEqual("abcde fghij", sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenLowercaseRevealDisabled_KeepsGeneratedTextCasing()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        var sut = CreateSut(practiceController, revealSentText: true, revealInLowercase: false);

        await sut.StartPracticeAsync();

        Assert.AreEqual("ABCDE FGHIJ", sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenLowercaseRevealEnabled_KeepsProsignsIntact()
    {
        var practiceController = CreateController();
        practiceController.CharacterSets.Returns(new List<KeyValuePair<string, string>>
        {
            new("Full", "AB<ar><sk>"),
        });
        practiceController.SelectedCharacterSet.Returns("Full");
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("AB<ar> CD<sk>");

        var sut = CreateSut(practiceController, revealSentText: true, revealInLowercase: true);

        await sut.StartPracticeAsync();

        Assert.AreEqual("ab<ar> cd<sk>", sut.ReceivedText);
    }

    [TestMethod]
    public async Task StartPracticeAsync_WhenLowercaseRevealEnabledButRevealDisabled_DoesNotFill()
    {
        var practiceController = CreateController();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns("ABCDE FGHIJ");

        var sut = CreateSut(practiceController, revealSentText: false, revealInLowercase: true);

        await sut.StartPracticeAsync();

        Assert.AreEqual(string.Empty, sut.ReceivedText);
    }

    [TestMethod]
    public async Task OpenResultWindowAsync_BuildsAndShowsPracticeResult()
    {
        var practiceController = CreateController();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        practiceController.LastUsedCharacterWpm.Returns(20);
        practiceController.LastUsedAverageWpm.Returns(15);
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, false, 10, Arg.Any<NoiseSettings>()).Returns(Task.FromResult(false));

        var sut = CreateSut(practiceController, resultWindowService);
        sut.ReceivedText = "RX";

        await sut.OpenResultWindowAsync();

        practiceController.Received(1).BuildResult("RX");
        await resultWindowService.Received(1).ShowPracticeResultAsync(result, 20, 15, false, 10, Arg.Any<NoiseSettings>());
    }

    [TestMethod]
    public async Task OpenResultWindowAsync_WhenSaved_MarksSessionSavedSoReopenPassesAlreadySaved()
    {
        var practiceController = CreateController();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        practiceController.LastUsedCharacterWpm.Returns(20);
        practiceController.LastUsedAverageWpm.Returns(15);
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, Arg.Any<bool>(), 10, Arg.Any<NoiseSettings>())
            .Returns(true, false);

        var sut = CreateSut(practiceController, resultWindowService);
        sut.ReceivedText = "RX";

        await sut.OpenResultWindowAsync();
        await sut.OpenResultWindowAsync();

        // The save state is the VM's own: the first open reports alreadySaved=false, the
        // reopen (same session, already saved) reports true.
        await resultWindowService.Received(1).ShowPracticeResultAsync(result, 20, 15, false, 10, Arg.Any<NoiseSettings>());
        await resultWindowService.Received(1).ShowPracticeResultAsync(result, 20, 15, true, 10, Arg.Any<NoiseSettings>());
    }

    [TestMethod]
    public async Task StartPracticeAsync_ResetsResultSavedStateForTheNewSession()
    {
        var practiceController = CreateController();
        var resultWindowService = Substitute.For<IPracticeResultWindowService>();
        practiceController.StartAsync().Returns(Task.CompletedTask);
        practiceController.LastGeneratedText.Returns(string.Empty);

        var result = new PracticeResult { CharacterCount = 10, ErrorCount = 1, ErrorRatePercent = 10, IsSuccessful = true };
        practiceController.BuildResult("RX").Returns(result);
        practiceController.CreateSettingsSnapshot().Returns(CreateConfig("Default", 5, 20, 15));
        practiceController.LastUsedCharacterWpm.Returns(20);
        practiceController.LastUsedAverageWpm.Returns(15);
        resultWindowService.ShowPracticeResultAsync(result, 20, 15, Arg.Any<bool>(), 10, Arg.Any<NoiseSettings>())
            .Returns(true, false);

        var sut = CreateSut(practiceController, resultWindowService);
        sut.ReceivedText = "RX";

        // Save the first session's result (alreadySaved=false -> true).
        await sut.OpenResultWindowAsync();

        // A new session resets the per-session save state (and clears ReceivedText), so the
        // next result open is not treated as already saved even though the prior session was.
        await sut.StartPracticeAsync();
        sut.ReceivedText = "RX";
        await sut.OpenResultWindowAsync();

        await resultWindowService.Received(2).ShowPracticeResultAsync(result, 20, 15, false, 10, Arg.Any<NoiseSettings>());
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

    private static PracticeViewModel CreateSut(
        IPracticeController practiceController,
        IPracticeResultWindowService? resultWindowService = null,
        bool revealSentText = true,
        bool revealInLowercase = false)
    {
        return new PracticeViewModel(
            practiceController,
            resultWindowService ?? Substitute.For<IPracticeResultWindowService>(),
            CreateConfigService(revealSentText, revealInLowercase),
            Substitute.For<ILogger<PracticeViewModel>>());
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
