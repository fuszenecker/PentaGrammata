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
        var result = _evaluator.Evaluate("<ar>", string.Empty, errorThresholdPercent: 100);

        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual(1, result.CharacterCount);
        Assert.AreEqual(100d, result.ErrorRatePercent, 0.0001);
        Assert.AreEqual("[-<ar>]", result.Rows[0].Difference);
    }

    [TestMethod]
    public void Evaluate_CountsProsignAsOneSymbolInMixedText()
    {
        var result = _evaluator.Evaluate("A<ar>B", "A<ar>B", errorThresholdPercent: 0);

        Assert.AreEqual(3, result.CharacterCount);
        Assert.AreEqual(0, result.ErrorCount);
        Assert.AreEqual(0d, result.ErrorRatePercent, 0.0001);
    }

    [TestMethod]
    [DataRow("<ar>")]
    [DataRow("<as>")]
    [DataRow("<bk>")]
    [DataRow("<bt>")]
    [DataRow("<kn>")]
    [DataRow("<sk>")]
    public void Evaluate_CountsEverySupportedProsignAsOneSymbol(string prosign)
    {
        var result = _evaluator.Evaluate(prosign, string.Empty, errorThresholdPercent: 100);

        Assert.AreEqual(1, result.CharacterCount);
        Assert.AreEqual(1, result.ErrorCount);
        Assert.AreEqual(100d, result.ErrorRatePercent, 0.0001);
        Assert.AreEqual($"[-{prosign}]", result.Rows[0].Difference);
    }

    private const string Joiner = "_";

    [TestMethod]
    public void Evaluate_MultiWordItem_IsOneRowAndTakesAsManyReceivedWords()
    {
        var result = _evaluator.Evaluate("CAPE" + Joiner + "TOWN WIEN", "cape town wien", errorThresholdPercent: 0);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("CAPE" + Joiner + "TOWN", result.Rows[0].SentGroup);
        Assert.AreEqual("cape" + Joiner + "town", result.Rows[0].ReceivedGroup);
        Assert.AreEqual("WIEN", result.Rows[1].SentGroup);
        Assert.AreEqual("wien", result.Rows[1].ReceivedGroup);
        Assert.AreEqual(12, result.CharacterCount, "The space inside an item is not a character.");
        Assert.AreEqual(0, result.ErrorCount);
    }

    [TestMethod]
    public void Evaluate_RepeatedItems_AreSeparateRowsAndEachIsChecked()
    {
        var result = _evaluator.Evaluate("LACI LACI WIEN WIEN", "laci laci wien wien", 0);

        Assert.HasCount(4, result.Rows);
        Assert.AreEqual(16, result.CharacterCount);
        Assert.AreEqual(0, result.ErrorCount);
    }

    [TestMethod]
    public void Evaluate_MissingRepetition_CountsAsErrors()
    {
        var result = _evaluator.Evaluate("LACI LACI", "LACI", 0);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual(8, result.CharacterCount);
        Assert.AreEqual(4, result.ErrorCount, "The second LACI was not copied.");
    }

    [TestMethod]
    public void Evaluate_RepeatedMultiWordItem_IsOneRowPerRepetition()
    {
        var result = _evaluator.Evaluate("NEW" + Joiner + "YORK NEW" + Joiner + "YORK", "new york new york", 0);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("NEW" + Joiner + "YORK", result.Rows[0].SentGroup);
        Assert.AreEqual("NEW" + Joiner + "YORK", result.Rows[1].SentGroup);
        Assert.AreEqual(14, result.CharacterCount);
        Assert.AreEqual(0, result.ErrorCount);
    }

    [TestMethod]
    public void Evaluate_ReceivedUnderscore_IsTheSameAsASpace()
    {
        var plain = _evaluator.Evaluate("NEW" + Joiner + "YORK", "NEW YORK", 0);
        var joined = _evaluator.Evaluate("NEW" + Joiner + "YORK", "NEW" + Joiner + "YORK", 0);

        Assert.AreEqual(0, plain.ErrorCount);
        Assert.AreEqual(0, joined.ErrorCount);
        Assert.AreEqual(plain.Rows[0].ReceivedGroup, joined.Rows[0].ReceivedGroup);
    }

    [TestMethod]
    public void Evaluate_MultiWordItem_IsScoredWordByWord()
    {
        var grouped = _evaluator.Evaluate("FRANKFURT" + Joiner + "AM" + Joiner + "MAIN ROMA", "FRANKFURT AN MAIN ROMX", 0);
        var separate = _evaluator.Evaluate("FRANKFURT AM MAIN ROMA", "FRANKFURT AN MAIN ROMX", 0);

        Assert.HasCount(2, grouped.Rows);
        Assert.AreEqual(2, grouped.ErrorCount);
        Assert.AreEqual(separate.ErrorCount, grouped.ErrorCount);
        Assert.AreEqual(separate.CharacterCount, grouped.CharacterCount);
        Assert.AreEqual("......... .[!M] ....", grouped.Rows[0].Difference, "One difference per word, space separated.");
    }

    [TestMethod]
    public void Evaluate_ShortCopy_LeavesTheRestOfTheItemEmpty()
    {
        var result = _evaluator.Evaluate("NEW" + Joiner + "YORK WIEN", "NEW", 0);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("NEW", result.Rows[0].ReceivedGroup);
        Assert.AreEqual(string.Empty, result.Rows[1].ReceivedGroup);
        Assert.AreEqual(8, result.ErrorCount);
    }

    [TestMethod]
    public void Evaluate_ExtraReceivedWords_AreOneRowEach()
    {
        var result = _evaluator.Evaluate("NEW" + Joiner + "YORK", "NEW YORK CITY NOW", 0);

        Assert.HasCount(3, result.Rows);
        Assert.AreEqual("CITY", result.Rows[1].ReceivedGroup);
        Assert.AreEqual(string.Empty, result.Rows[1].SentGroup);
        Assert.AreEqual(7, result.ErrorCount);
    }
}
