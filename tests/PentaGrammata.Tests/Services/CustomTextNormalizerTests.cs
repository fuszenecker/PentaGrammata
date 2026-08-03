using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class CustomTextNormalizerTests
{
    [TestMethod]
    public void Normalize_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, CustomTextNormalizer.Normalize(null));
        Assert.AreEqual(string.Empty, CustomTextNormalizer.Normalize(string.Empty));
        Assert.AreEqual(string.Empty, CustomTextNormalizer.Normalize("  \r\n\t "));
    }

    [TestMethod]
    public void Normalize_CollapsesRunsOfWhitespaceToSingleSpaces()
    {
        Assert.AreEqual("CQ CQ DE HA5XYZ", CustomTextNormalizer.Normalize("CQ   CQ\tDE\r\n\nHA5XYZ"));
    }

    [TestMethod]
    public void Normalize_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.AreEqual("CQ DE", CustomTextNormalizer.Normalize("\r\n  CQ DE  \r\n"));
    }

    [TestMethod]
    public void Normalize_LeavesAlreadyNormalizedTextUnchanged()
    {
        Assert.AreEqual("CQ DE HA5XYZ <ar>", CustomTextNormalizer.Normalize("CQ DE HA5XYZ <ar>"));
    }
}
