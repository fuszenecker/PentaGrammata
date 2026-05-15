using Microsoft.VisualStudio.TestTools.UnitTesting;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeResultEvaluatorTests
{
    private readonly PracticeResultEvaluator _evaluator = new();

    [TestMethod]
    public void Evaluate_CaseInsensitiveExactMatch_HasNoErrors()
    {
        var result = _evaluator.Evaluate("ABC DE", "abc de", errorThresholdPercent: 0);

        Assert.AreEqual(5, result.CharacterCount);
        Assert.AreEqual(0, result.ErrorCount);
        Assert.AreEqual(0d, result.ErrorRatePercent, 0.0001);
        Assert.IsTrue(result.IsSuccessful);
        Assert.AreEqual(2, result.Rows.Count);
        Assert.AreEqual("...", result.Rows[0].Difference);
        Assert.AreEqual("..", result.Rows[1].Difference);
    }

    [TestMethod]
    public void Evaluate_SingleSubstitution_ReportsOneError()
    {
        var result = _evaluator.Evaluate("ABCDE", "ABXDE", errorThresholdPercent: 19.9);

        Assert.AreEqual(5, result.CharacterCount);
        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual(20d, result.ErrorRatePercent, 0.0001);
        Assert.IsFalse(result.IsSuccessful);
        Assert.AreEqual("..C..", result.Rows[0].Difference);
    }

    [TestMethod]
    public void Evaluate_ExtraReceivedGroup_AddsInsertedTokenAndErrors()
    {
        var result = _evaluator.Evaluate("ABCD", "ABCD EFGH", errorThresholdPercent: 100);

        Assert.AreEqual(4, result.CharacterCount);
        Assert.AreEqual(4, result.ErrorCount);
        Assert.AreEqual(100d, result.ErrorRatePercent, 0.0001);
        Assert.IsTrue(result.IsSuccessful);
        Assert.AreEqual(2, result.Rows.Count);
        Assert.AreEqual("[+EFGH]", result.Rows[1].Difference);
    }
}
