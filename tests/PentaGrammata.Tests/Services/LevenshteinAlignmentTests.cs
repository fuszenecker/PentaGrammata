using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class LevenshteinAlignmentTests
{
    [TestMethod]
    public void GetDistance_TreatsSpecialSequenceAsSingleSymbol()
    {
        var distance = LevenshteinAlignment.GetDistance("<bk>", "X");

        Assert.AreEqual(1, distance);
    }

    [TestMethod]
    public void Align_SpecialSequenceSubstitution_ProducesSingleSubstituteEdit()
    {
        var edits = LevenshteinAlignment.Align("<bk>", "X");

        Assert.HasCount(1, edits);
        Assert.AreEqual(LevenshteinEditKind.Substitute, edits[0].Kind);
        Assert.AreEqual("<bk>", edits[0].Expected);
        Assert.AreEqual("X", edits[0].Actual);
    }

    [TestMethod]
    public void Align_MixedDigitsAndDeletion_ProducesExpectedEditSequence()
    {
        var edits = LevenshteinAlignment.Align("12345", "2234");

        Assert.HasCount(5, edits);
        Assert.AreEqual(LevenshteinEditKind.Substitute, edits[0].Kind);
        Assert.AreEqual("1", edits[0].Expected);
        Assert.AreEqual("2", edits[0].Actual);

        Assert.AreEqual(LevenshteinEditKind.Match, edits[1].Kind);
        Assert.AreEqual(LevenshteinEditKind.Match, edits[2].Kind);
        Assert.AreEqual(LevenshteinEditKind.Match, edits[3].Kind);

        Assert.AreEqual(LevenshteinEditKind.Delete, edits[4].Kind);
        Assert.AreEqual("5", edits[4].Expected);
        Assert.AreEqual(string.Empty, edits[4].Actual);
    }
}
