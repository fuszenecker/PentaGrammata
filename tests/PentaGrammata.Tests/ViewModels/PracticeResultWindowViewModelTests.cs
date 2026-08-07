using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Configuration;
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
        var statisticsService = Substitute.For<IPracticeResultStatisticsService>();
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
                    Difference = "..[!C]",
                },
            ],
        };

        var sut = new PracticeResultWindowViewModel(result, 20, 15, false, 10.0, new NoiseSettings(), statisticsService, infoDialogService);

        Assert.HasCount(1, sut.Rows);
        Assert.AreEqual("ABC", sut.Rows[0].SentGroup);
        Assert.AreEqual("ABD", sut.Rows[0].ReceivedGroup);
        Assert.AreEqual("12", sut.CharacterCountText);
        Assert.AreEqual("3", sut.ErrorsText);
        Assert.AreEqual("25.00%", sut.ErrorRateText);
        Assert.AreEqual(StatusLevel.Error, sut.ResultStatus);
    }

    [TestMethod]
    public void Constructor_ParsesDifferenceIntoColoredSegments()
    {
        var statisticsService = Substitute.For<IPracticeResultStatisticsService>();
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
                    Difference = ".[+yz][-q][!A] ",
                },
            ],
        };

        var sut = new PracticeResultWindowViewModel(result, 20, 15, false, 10.0, new NoiseSettings(), statisticsService, infoDialogService);
        var segments = sut.Rows[0].DifferenceSegments;

        Assert.HasCount(5, segments);
        Assert.AreEqual(".", segments[0].Text);
        Assert.AreEqual(DiffSegmentKind.Unchanged, segments[0].Kind);

        Assert.AreEqual("yz", segments[1].Text);
        Assert.AreEqual(DiffSegmentKind.Inserted, segments[1].Kind);

        Assert.AreEqual("q", segments[2].Text);
        Assert.AreEqual(DiffSegmentKind.Deleted, segments[2].Kind);

        Assert.AreEqual("A", segments[3].Text);
        Assert.AreEqual(DiffSegmentKind.Substituted, segments[3].Kind);

        Assert.AreEqual(" ", segments[4].Text);
        Assert.AreEqual(DiffSegmentKind.Unchanged, segments[4].Kind);
        Assert.AreEqual(StatusLevel.Success, sut.ResultStatus);
    }

    [TestMethod]
    public void Constructor_SubstitutedPunctuation_IsColoredAsSubstitutedNotUnchanged()
    {
        // Regression: a substituted "." used to parse as the "match" marker and render
        // gray, making punctuation errors invisible in the differences column.
        var sut = BuildViewModelForDifference(".[!.][!,].");
        var segments = sut.Rows[0].DifferenceSegments;

        Assert.HasCount(4, segments);
        Assert.AreEqual(".", segments[0].Text);
        Assert.AreEqual(DiffSegmentKind.Unchanged, segments[0].Kind);

        Assert.AreEqual(".", segments[1].Text);
        Assert.AreEqual(DiffSegmentKind.Substituted, segments[1].Kind);

        Assert.AreEqual(",", segments[2].Text);
        Assert.AreEqual(DiffSegmentKind.Substituted, segments[2].Kind);

        Assert.AreEqual(".", segments[3].Text);
        Assert.AreEqual(DiffSegmentKind.Unchanged, segments[3].Kind);
    }

    [TestMethod]
    public void Constructor_SubstitutedProsign_IsKeptAsOneSubstitutedSegment()
    {
        var sut = BuildViewModelForDifference("[!<bk>]");
        var segments = sut.Rows[0].DifferenceSegments;

        Assert.HasCount(1, segments);
        Assert.AreEqual("<bk>", segments[0].Text);
        Assert.AreEqual(DiffSegmentKind.Substituted, segments[0].Kind);
    }

    [TestMethod]
    public async Task SaveResultsCommand_SavesOnce_AndDisablesAfterCompletion()
    {
        var statisticsService = Substitute.For<IPracticeResultStatisticsService>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        statisticsService.DatabasePath.Returns("/tmp/practice-results.db");
        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, false, 10.0, new NoiseSettings(), statisticsService, infoDialogService);

        Assert.IsTrue(sut.SaveResultsCommand.CanExecute(null));

        await sut.SaveResultsCommand.ExecuteAsync(null);

        await statisticsService.Received(1).SaveAsync(Arg.Any<PracticeResultStatisticsRecord>(), Arg.Any<CancellationToken>());
        await infoDialogService.Received(1).ShowInfoAsync("Results saved", "Statistics were saved to:\n/tmp/practice-results.db", "ResultsSaved", "Database location");
        Assert.IsTrue(sut.IsSaveCompleted);
        Assert.IsFalse(sut.SaveResultsCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SaveResultsCommand_WhenSaveFails_ShowsErrorAndKeepsSaveEnabled()
    {
        var statisticsService = Substitute.For<IPracticeResultStatisticsService>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        statisticsService.SaveAsync(Arg.Any<PracticeResultStatisticsRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new StatisticsStoreException(
                "Could not save practice statistics.",
                new IOException("Database is locked"))));

        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, false, 10.0, new NoiseSettings(), statisticsService, infoDialogService);

        await sut.SaveResultsCommand.ExecuteAsync(null);

        Assert.IsFalse(sut.IsSaveCompleted);
        Assert.IsFalse(sut.IsSaving);
        Assert.IsTrue(sut.SaveResultsCommand.CanExecute(null));
        await infoDialogService.Received(1).ShowInfoAsync("Save failed", "Could not save statistics:\nDatabase is locked");
    }

    [TestMethod]
    public void Constructor_WhenAlreadySaved_SaveCommandIsDisabled()
    {
        var statisticsService = Substitute.For<IPracticeResultStatisticsService>();
        var infoDialogService = Substitute.For<IInfoDialogService>();
        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, true, 10.0, new NoiseSettings(), statisticsService, infoDialogService);

        Assert.IsTrue(sut.IsSaveCompleted);
        Assert.IsFalse(sut.SaveResultsCommand.CanExecute(null));
    }

    private static PracticeResultWindowViewModel BuildViewModelForDifference(string difference)
    {
        var result = new PracticeResult
        {
            CharacterCount = 5,
            ErrorCount = 1,
            ErrorRatePercent = 20,
            IsSuccessful = true,
            Rows = [new PracticeResultRow { SentGroup = "a", ReceivedGroup = "b", Difference = difference }],
        };

        return new PracticeResultWindowViewModel(
            result,
            20,
            15,
            false,
            10.0,
            new NoiseSettings(),
            Substitute.For<IPracticeResultStatisticsService>(),
            Substitute.For<IInfoDialogService>());
    }
}
