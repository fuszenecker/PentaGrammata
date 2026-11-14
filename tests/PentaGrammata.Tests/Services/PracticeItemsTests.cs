using Microsoft.VisualStudio.TestTools.UnitTesting;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeItemsTests
{
    private const string Joiner = "_";

    [TestMethod]
    [DataRow("NEW YORK", "NEW" + Joiner + "YORK")]
    [DataRow("  FRANKFURT   AM\tMAIN ", "FRANKFURT" + Joiner + "AM" + Joiner + "MAIN")]
    [DataRow("DEN" + Joiner + "HAAG", "DEN" + Joiner + "HAAG")]
    [DataRow("WIEN", "WIEN")]
    public void Join_CollapsesAnyWhitespaceIntoUnderscores(string entry, string expected)
    {
        Assert.AreEqual(expected, PracticeItems.Join(entry));
    }

    [TestMethod]
    public void SplitItems_SplitsAtOrdinarySpacesOnly()
    {
        CollectionAssert.AreEqual(
            new[] { "NEW" + Joiner + "YORK", "WIEN", "QSL" + Joiner + "VIA" + Joiner + "BURO" },
            PracticeItems.SplitItems("NEW" + Joiner + "YORK  WIEN\nQSL" + Joiner + "VIA" + Joiner + "BURO "));
    }

    [TestMethod]
    public void SplitItems_DropsStrayUnderscores()
    {
        CollectionAssert.AreEqual(new[] { "A" }, PracticeItems.SplitItems(Joiner + "A" + Joiner + " " + Joiner));
        Assert.IsEmpty(PracticeItems.SplitItems(null));
    }

    [TestMethod]
    public void SplitWords_TreatsBothSpacesAlike()
    {
        CollectionAssert.AreEqual(new[] { "NEW", "YORK", "WIEN" }, PracticeItems.SplitWords("NEW" + Joiner + "YORK WIEN"));
    }

    [TestMethod]
    public void WordsAndToPlain_UndoTheJoin()
    {
        var item = PracticeItems.Join("LITTLE JOHN");

        CollectionAssert.AreEqual(new[] { "LITTLE", "JOHN" }, PracticeItems.Words(item));
        Assert.AreEqual("LITTLE JOHN WIEN", PracticeItems.ToPlain(item + " WIEN"));
    }
}
