using System.Linq;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class MorseAlphabetTests
{
    [TestMethod]
    public void GetSymbols_KnownTokens_ReturnElements()
    {
        Assert.AreEqual(".-", MorseAlphabet.GetSymbols("a"));
        Assert.AreEqual("-----", MorseAlphabet.GetSymbols("0"));
        Assert.AreEqual("...-.-", MorseAlphabet.GetSymbols("<sk>"));
    }

    [TestMethod]
    public void GetSymbols_UnknownToken_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MorseAlphabet.GetSymbols("!"));
        Assert.AreEqual(string.Empty, MorseAlphabet.GetSymbols("<zz>"));
        // The table is keyed on lower case; GetSymbols does not fold case itself.
        Assert.AreEqual(string.Empty, MorseAlphabet.GetSymbols("A"));
    }

    [TestMethod]
    public void Supports_IsCaseInsensitive()
    {
        Assert.IsTrue(MorseAlphabet.Supports("A"));
        Assert.IsTrue(MorseAlphabet.Supports("a"));
        Assert.IsTrue(MorseAlphabet.Supports("<AR>"));
        Assert.IsTrue(MorseAlphabet.Supports(" "));
        Assert.IsFalse(MorseAlphabet.Supports("!"));
    }

    [TestMethod]
    public void Tokenize_KeepsProsignsWhole()
    {
        CollectionAssert.AreEqual(
            new[] { "C", "Q", " ", "<ar>" },
            MorseAlphabet.Tokenize("CQ <ar>").ToList());
    }

    [TestMethod]
    public void Tokenize_UnterminatedAngleBracket_YieldsLoneCharacter()
    {
        CollectionAssert.AreEqual(
            new[] { "<", "a", "r" },
            MorseAlphabet.Tokenize("<ar").ToList());
    }
}
