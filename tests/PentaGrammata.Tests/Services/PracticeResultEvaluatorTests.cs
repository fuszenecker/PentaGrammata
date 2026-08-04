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
        Assert.HasCount(2, result.Rows);
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
        Assert.AreEqual("..[!C]..", result.Rows[0].Difference);
    }

    [TestMethod]
    public void Evaluate_ExtraReceivedGroup_AddsInsertedTokenAndErrors()
    {
        var result = _evaluator.Evaluate("ABCD", "ABCD EFGH", errorThresholdPercent: 100);

        Assert.AreEqual(4, result.CharacterCount);
        Assert.AreEqual(4, result.ErrorCount);
        Assert.AreEqual(100d, result.ErrorRatePercent, 0.0001);
        Assert.IsTrue(result.IsSuccessful);
        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("[+EFGH]", result.Rows[1].Difference);
    }

    [TestMethod]
    public void Evaluate_SpecialTokenSubstitution_KeepsTokenIntactInDiff()
    {
        var result = _evaluator.Evaluate("<bk>", "X", errorThresholdPercent: 100);

        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual("[!<bk>]", result.Rows[0].Difference);
    }

    [TestMethod]
    public void Evaluate_SubstitutedPeriod_IsDistinguishableFromAMatch()
    {
        // Regression: "." was emitted raw for substitutions, so a wrongly-copied period
        // produced the exact same diff text as a perfect match and vanished from the view.
        var substituted = _evaluator.Evaluate("A.C", "A,C", errorThresholdPercent: 100);
        var perfect = _evaluator.Evaluate("A.C", "A.C", errorThresholdPercent: 100);

        Assert.AreEqual(1, substituted.ErrorCount);
        Assert.AreEqual("...", perfect.Rows[0].Difference);
        Assert.AreEqual(".[!.].", substituted.Rows[0].Difference);
        Assert.AreNotEqual(perfect.Rows[0].Difference, substituted.Rows[0].Difference);
    }

    [TestMethod]
    [DataRow(".", ".[!.].")]
    [DataRow(",", ".[!,].")]
    [DataRow("/", ".[!/].")]
    [DataRow("+", ".[!+].")]
    [DataRow("?", ".[!?].")]
    [DataRow("=", ".[!=].")]
    public void Evaluate_SubstitutedPunctuation_IsMarkedAsSubstituted(string sentSymbol, string expectedDifference)
    {
        var result = _evaluator.Evaluate($"A{sentSymbol}C", "AXC", errorThresholdPercent: 100);

        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual(expectedDifference, result.Rows[0].Difference);
    }

    [TestMethod]
    public void Evaluate_SpecialTokenDeletion_ReportsSingleDeletedToken()
    {
        var result = _evaluator.Evaluate("<bk>", string.Empty, errorThresholdPercent: 100);

        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual("[-<bk>]", result.Rows[0].Difference);
    }
}
