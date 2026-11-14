using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class TopicListTextTests
{
    [TestMethod]
    public void Parse_TrimsEntriesDropsBlankLinesAndSorts()
    {
        CollectionAssert.AreEqual(new[] { "CAPE TOWN", "HA#xyz", "QRZ" }, TopicListText.Parse("  HA#xyz \r\n\n CAPE TOWN\n   \nQRZ"));
    }

    [TestMethod]
    public void Parse_DropsRepeatsIgnoringCase()
    {
        CollectionAssert.AreEqual(new[] { "Laci", "PISTA" }, TopicListText.Parse("PISTA\nLaci\nLACI\npista"));
    }

    [TestMethod]
    public void Format_ShowsTheListInAlphabeticalOrder()
    {
        Assert.AreEqual(string.Join(Environment.NewLine, "ab", "B", "c"), TopicListText.Format(["c", "B", "ab"]));
    }

    [TestMethod]
    public void Parse_OfNullOrEmptyIsEmpty()
    {
        Assert.IsEmpty(TopicListText.Parse(null));
        Assert.IsEmpty(TopicListText.Parse(string.Empty));
    }

    [TestMethod]
    public void Format_PutsOneEntryPerLine_AndParsesBack()
    {
        var text = TopicListText.Format(["A", "B C"]);

        Assert.AreEqual("A" + Environment.NewLine + "B C", text);
        CollectionAssert.AreEqual(new[] { "A", "B C" }, TopicListText.Parse(text));
    }
}
