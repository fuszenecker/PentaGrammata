using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class ConfusionObservationExtractorTests
{
    [TestMethod]
    public void Extract_SpecialTokenSubstitution_StoresSingleNormalizedObservation()
    {
        var recordedAt = DateTimeOffset.UtcNow;
        var rows = new[]
        {
            new PracticeResultRow
            {
                SentGroup = "<BK>",
                ReceivedGroup = "x",
                Difference = "ignored",
            },
        };

        var observations = new ConfusionObservationExtractor().Extract(rows, recordedAt);

        Assert.HasCount(1, observations);
        Assert.AreEqual("<bk>", observations[0].ExpectedSymbol);
        Assert.AreEqual("X", observations[0].ActualSymbol);
        Assert.AreEqual(1, observations[0].Count);
        Assert.AreEqual(1, observations[0].Distance);
    }

    [TestMethod]
    public void Extract_RepeatedSamePair_AggregatesCounts()
    {
        var recordedAt = DateTimeOffset.UtcNow;
        var rows = new[]
        {
            new PracticeResultRow { SentGroup = "<bk>", ReceivedGroup = "X", Difference = string.Empty },
            new PracticeResultRow { SentGroup = "<bk>", ReceivedGroup = "X", Difference = string.Empty },
        };

        var observations = new ConfusionObservationExtractor().Extract(rows, recordedAt);

        Assert.HasCount(1, observations);
        Assert.AreEqual("<bk>", observations[0].ExpectedSymbol);
        Assert.AreEqual("X", observations[0].ActualSymbol);
        Assert.AreEqual(2, observations[0].Count);
    }

    [TestMethod]
    public void Extract_MultiWordItem_IsComparedWordByWord()
    {
        var rows = new[]
        {
            new PracticeResultRow { SentGroup = "NEW_YORK", ReceivedGroup = "NEW_YORX", Difference = "ignored" },
        };

        var observations = new ConfusionObservationExtractor().Extract(rows, DateTimeOffset.UtcNow);

        Assert.HasCount(1, observations);
        Assert.AreEqual("K", observations[0].ExpectedSymbol);
        Assert.AreEqual("X", observations[0].ActualSymbol);
    }

    [TestMethod]
    public void Extract_MultiWordItemWithAMissingWord_CountsTheWordAsGaps()
    {
        var rows = new[]
        {
            new PracticeResultRow { SentGroup = "NEW_YORK", ReceivedGroup = "NEW", Difference = "ignored" },
        };

        var observations = new ConfusionObservationExtractor().Extract(rows, DateTimeOffset.UtcNow);

        Assert.IsTrue(observations.All(o => o.ActualSymbol == "_"));
        Assert.AreEqual(4, observations.Sum(o => o.Count));
        Assert.IsFalse(observations.Any(o => o.ExpectedSymbol == "_"));
    }
}
