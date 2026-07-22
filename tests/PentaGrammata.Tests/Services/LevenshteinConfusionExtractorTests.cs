using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class LevenshteinConfusionExtractorTests
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

        var observations = LevenshteinConfusionExtractor.Extract(rows, recordedAt);

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

        var observations = LevenshteinConfusionExtractor.Extract(rows, recordedAt);

        Assert.HasCount(1, observations);
        Assert.AreEqual("<bk>", observations[0].ExpectedSymbol);
        Assert.AreEqual("X", observations[0].ActualSymbol);
        Assert.AreEqual(2, observations[0].Count);
    }
}
