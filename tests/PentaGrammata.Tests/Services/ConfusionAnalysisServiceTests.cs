using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class ConfusionAnalysisServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
    private readonly ConfusionAnalysisService _sut = new();

    [TestMethod]
    public void BuildMatrix_WithNoObservations_ReturnsNoSubstitutionData()
    {
        var result = _sut.BuildMatrix([], 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.NoSubstitutionData, result.Status);
        Assert.IsNull(result.Matrix);
    }

    [TestMethod]
    public void BuildMatrix_WithOnlyGapSymbols_ReturnsNoSubstitutionData()
    {
        var observations = new[]
        {
            Obs("_", "A", 5, 1, Now),
            Obs("A", "_", 5, 1, Now),
        };

        var result = _sut.BuildMatrix(observations, 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.NoSubstitutionData, result.Status);
    }

    [TestMethod]
    public void BuildMatrix_WhenEveryScoreDecaysToZero_ReturnsNoVisibleAfterWeighting()
    {
        // One half-life of 1 day; an observation 10000 days old has decayed to ~0.
        var observations = new[]
        {
            Obs("A", "B", 5, 1, Now.AddDays(-10000)),
        };

        var result = _sut.BuildMatrix(observations, 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.NoVisibleAfterWeighting, result.Status);
    }

    [TestMethod]
    public void BuildMatrix_BuildsMatrixWithExpectedSymbolsScoresAndTotals()
    {
        var observations = new[]
        {
            Obs("A", "B", 5, 1, Now),
            Obs("C", "D", 3, 1, Now),
        };

        var result = _sut.BuildMatrix(observations, 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.Available, result.Status);
        var matrix = result.Matrix!;
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, matrix.Symbols.ToArray());
        Assert.AreEqual(5, matrix.Cells[0, 1]); // A -> B
        Assert.AreEqual(3, matrix.Cells[2, 3]); // C -> D
        Assert.AreEqual(8, matrix.TotalScore);
        Assert.AreEqual(5, matrix.MaxScore);
    }

    [TestMethod]
    public void BuildMatrix_WeighsOlderObservationsByHalfLife()
    {
        // A fresh A->B (count 10) plus one a single half-life old (count 10, expected weight 5).
        var observations = new[]
        {
            Obs("A", "B", 10, 1, Now),
            Obs("A", "B", 10, 1, Now.AddDays(-1)),
        };

        var result = _sut.BuildMatrix(observations, 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.Available, result.Status);
        // 10 (fresh) + 5 (half-weighted) = 15 in the A -> B cell.
        Assert.AreEqual(15, result.Matrix!.Cells[0, 1]);
    }

    [TestMethod]
    public void BuildMatrix_LimitsSymbolsToTheTopSet()
    {
        // 30 distinct symbols so only the top 24 by score are kept.
        var observations = Enumerable.Range(0, 30)
            .Select(i => Obs($"E{i}", $"A{i}", 30 - i, 1, Now))
            .ToArray();

        var result = _sut.BuildMatrix(observations, 1, Now);

        Assert.AreEqual(ConfusionMatrixStatus.Available, result.Status);
        Assert.HasCount(24, result.Matrix!.Symbols);
    }

    [TestMethod]
    public void WeightedSymbolCounts_CountsBothExpectedAndActualSymbols()
    {
        var observations = new[]
        {
            Obs("A", "B", 4, 1, Now),
        };

        var counts = _sut.WeightedSymbolCounts(observations, 1, Now);

        Assert.HasCount(2, counts);
        Assert.AreEqual(4, counts.Single(c => c.Symbol == "A").Score);
        Assert.AreEqual(4, counts.Single(c => c.Symbol == "B").Score);
    }

    [TestMethod]
    public void BuildPracticeConfusionsCharacterSet_ReturnsNullWhenNoData()
    {
        Assert.IsNull(_sut.BuildPracticeConfusionsCharacterSet([], 1, Now, 200));
    }

    [TestMethod]
    public void BuildPracticeConfusionsCharacterSet_ScalesSymbolsToTargetCount()
    {
        // A->B (count 10) and C->D (count 5): per-symbol weights A=10, B=10, C=5, D=5 (total 30).
        var observations = new[]
        {
            Obs("A", "B", 10, 1, Now),
            Obs("C", "D", 5, 1, Now),
        };

        var characterSet = _sut.BuildPracticeConfusionsCharacterSet(observations, 1, Now, 10);

        Assert.IsNotNull(characterSet);
        // target = max(4, 10) = 10 entries, scaled by weight: A=3, B=3, C=2, D=2.
        Assert.AreEqual(10, characterSet!.Length);
        Assert.AreEqual(3, characterSet.Count(c => c == 'A'));
        Assert.AreEqual(3, characterSet.Count(c => c == 'B'));
        Assert.AreEqual(2, characterSet.Count(c => c == 'C'));
        Assert.AreEqual(2, characterSet.Count(c => c == 'D'));
    }

    private static ConfusionObservation Obs(string expected, string actual, int count, int distance, DateTimeOffset recordedAt)
        => new()
        {
            ExpectedSymbol = expected,
            ActualSymbol = actual,
            Count = count,
            Distance = distance,
            RecordedAt = recordedAt,
        };
}
