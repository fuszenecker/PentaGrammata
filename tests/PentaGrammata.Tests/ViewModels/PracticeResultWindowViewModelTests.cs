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

        var sut = new PracticeResultWindowViewModel(result, 20, 15, statisticsStore);

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

        var sut = new PracticeResultWindowViewModel(result, 20, 15, statisticsStore);
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
        var result = new PracticeResult
        {
            CharacterCount = 8,
            ErrorCount = 2,
            ErrorRatePercent = 25,
            IsSuccessful = false,
        };

        var sut = new PracticeResultWindowViewModel(result, 24, 18, statisticsStore);

        Assert.IsTrue(sut.SaveResultsCommand.CanExecute(null));

        await sut.SaveResultsCommand.ExecuteAsync(null);

        await statisticsStore.Received(1).SaveAsync(Arg.Any<PracticeResultStatisticsRecord>(), Arg.Any<CancellationToken>());
        Assert.IsTrue(sut.IsSaveCompleted);
        Assert.IsFalse(sut.SaveResultsCommand.CanExecute(null));
    }
}
