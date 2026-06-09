using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class PracticeResultWindowViewModelTests
{
    [TestMethod]
    public void Constructor_MapsRowsAndSummaryFields()
    {
        var statisticsStore = Substitute.For<IPracticeResultStatisticsStore>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        var result = new PracticeResult
        {
            CharacterCount = 12,
            ErrorCount = 3,
            ErrorRatePercent = 25.0,
            IsSuccessful = false,
            Rows =
            [
                new PracticeResultRow
                {
                    SentGroup = "abc",
                    ReceivedGroup = "abd",
                    Difference = "..C",
                },
            ],
        };

        var sut = new PracticeResultWindowViewModel(result, 20, 15, false, statisticsStore, infoDialogService);

        Assert.AreEqual(1, sut.Rows.Count);
        Assert.AreEqual("ABC", sut.Rows[0].SentGroup);
        Assert.AreEqual("ABD", sut.Rows[0].ReceivedGroup);
        Assert.AreEqual("12", sut.CharacterCountText);
        Assert.AreEqual("3", sut.ErrorsText);
        Assert.AreEqual("25.00%", sut.ErrorRateText);
        Assert.AreSame(Brushes.IndianRed, sut.ResultForeground);
    }

    [TestMethod]
    public void Constructor_ParsesDifferenceIntoColoredSegments()
    {
        var statisticsStore = Substitute.For<IPracticeResultStatisticsStore>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        var result = new PracticeResult
        {
            CharacterCount = 5,
            ErrorCount = 1,
            ErrorRatePercent = 20,
            IsSuccessful = true,
            Rows =
            [
                new PracticeResultRow
                {
                    SentGroup = "abc",
                    ReceivedGroup = "axc",
                    Difference = ".[+yz][-q]A ",
                },
            ],
        };

        var sut = new PracticeResultWindowViewModel(result, 20, 15, false, statisticsStore, infoDialogService);
        var segments = sut.Rows[0].DifferenceSegments;

        Assert.AreEqual(5, segments.Count);
        Assert.AreEqual(".", segments[0].Text);
        Assert.AreSame(Brushes.Gainsboro, segments[0].Foreground);

        Assert.AreEqual("yz", segments[1].Text);
        Assert.AreSame(Brushes.LimeGreen, segments[1].Foreground);

        Assert.AreEqual("q", segments[2].Text);
        Assert.AreSame(Brushes.IndianRed, segments[2].Foreground);

        Assert.AreEqual("A", segments[3].Text);
        Assert.AreSame(Brushes.Gold, segments[3].Foreground);

        Assert.AreEqual(" ", segments[4].Text);
        Assert.AreSame(Brushes.Gainsboro, segments[4].Foreground);
        Assert.AreSame(Brushes.LimeGreen, sut.ResultForeground);
    }

    [TestMethod]
    public async Task SaveResultsCommand_SavesOnce_AndDisablesAfterCompletion()
    {
        var statisticsStore = Substitute.For<IPracticeResultStatisticsStore>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        statisticsStore.DatabasePath.Returns("/tmp/practice-results.db");
        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, false, statisticsStore, infoDialogService);

        Assert.IsTrue(sut.SaveResultsCommand.CanExecute(null));

        await sut.SaveResultsCommand.ExecuteAsync(null);

        await statisticsStore.Received(1).SaveAsync(Arg.Any<PracticeResultStatisticsRecord>(), Arg.Any<CancellationToken>());
        await infoDialogService.Received(1).ShowInfoAsync("Results saved", "Statistics were saved to:\n/tmp/practice-results.db");
        Assert.IsTrue(sut.IsSaveCompleted);
        Assert.IsFalse(sut.SaveResultsCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SaveResultsCommand_WhenSaveFails_ShowsErrorAndKeepsSaveEnabled()
    {
        var statisticsStore = Substitute.For<IPracticeResultStatisticsStore>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        statisticsStore.SaveAsync(Arg.Any<PracticeResultStatisticsRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Database is locked")));

        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, false, statisticsStore, infoDialogService);

        await sut.SaveResultsCommand.ExecuteAsync(null);

        Assert.IsFalse(sut.IsSaveCompleted);
        Assert.IsFalse(sut.IsSaving);
        Assert.IsTrue(sut.SaveResultsCommand.CanExecute(null));
        await infoDialogService.Received(1).ShowInfoAsync("Save failed", "Could not save statistics:\nDatabase is locked");
    }

    [TestMethod]
    public void Constructor_WhenAlreadySaved_SaveCommandIsDisabled()
    {
        var statisticsStore = Substitute.For<IPracticeResultStatisticsStore>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, true, statisticsStore, infoDialogService);

        Assert.IsTrue(sut.IsSaveCompleted);
        Assert.IsFalse(sut.SaveResultsCommand.CanExecute(null));
    }
}
