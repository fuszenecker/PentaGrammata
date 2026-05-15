using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PentaGrammata.Configuration;

namespace PentaGrammata.Tests.Configuration;

[TestClass]
public sealed class CharacterSetTextCodecTests
{
    [TestMethod]
    public void FormatForEditor_SortsCaseInsensitive_AndFormatsLines()
    {
        var characterSets = new Dictionary<string, string>
        {
            ["zulu"] = "Z",
            ["Alpha"] = "A",
            ["beta"] = "B",
        };

        var result = CharacterSetTextCodec.FormatForEditor(characterSets);

        var lines = result.Split(Environment.NewLine, StringSplitOptions.None);
        CollectionAssert.AreEqual(new[] { "Alpha = A", "beta = B", "zulu = Z" }, lines);
    }

    [TestMethod]
    public void TryParse_ValidText_IgnoresCommentsAndWhitespace()
    {
        const string text = "# first line comment\n\nAlpha = ABC\n beta = DEF \n# trailing comment";

        var success = CharacterSetTextCodec.TryParse(text, out var parsedSets, out var error);

        Assert.IsTrue(success);
        Assert.AreEqual(string.Empty, error);
        Assert.AreEqual(2, parsedSets.Count);
        Assert.AreEqual("ABC", parsedSets["Alpha"]);
        Assert.AreEqual("DEF", parsedSets["beta"]);
    }

    [TestMethod]
    public void TryParse_InvalidLineWithoutSeparator_ReturnsError()
    {
        const string text = "Alpha ABC";

        var success = CharacterSetTextCodec.TryParse(text, out var parsedSets, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual(0, parsedSets.Count);
        Assert.AreEqual("Character set lines must use Name = Value format.", error);
    }

    [TestMethod]
    public void TryParse_NoValidEntries_ReturnsError()
    {
        const string text = "# only comment\n\n";

        var success = CharacterSetTextCodec.TryParse(text, out var parsedSets, out var error);

        Assert.IsFalse(success);
        Assert.AreEqual(0, parsedSets.Count);
        Assert.AreEqual("At least one character set is required.", error);
    }
}
