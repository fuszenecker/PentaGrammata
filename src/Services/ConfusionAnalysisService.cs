using System;
using System.Collections.Generic;
using System.Linq;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// Pure implementation of <see cref="IConfusionAnalysisService"/>. Scoring, symbol selection
/// and matrix construction live here so the view model is free of analysis math.
/// </summary>
public sealed class ConfusionAnalysisService : IConfusionAnalysisService
{
    private const string GapSymbol = "_";
    private const int TopSymbolsLimit = 24;

    public ConfusionMatrixResult BuildMatrix(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now)
    {
        var weighted = WeightedItems(observations, halfLifeDays, now);
        if (weighted.Count == 0)
        {
            return new ConfusionMatrixResult { Status = ConfusionMatrixStatus.NoSubstitutionData };
        }

        var positive = weighted.Where(x => x.Score > 0).ToArray();
        if (positive.Length == 0)
        {
            return new ConfusionMatrixResult { Status = ConfusionMatrixStatus.NoVisibleAfterWeighting };
        }

        // A symbol qualifies for the matrix if it appears as either an expected or an actual
        // symbol; its column/row total is the sum of both roles.
        var symbolTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in positive)
        {
            AddScore(symbolTotals, item.ExpectedSymbol, item.Score);
            AddScore(symbolTotals, item.ActualSymbol, item.Score);
        }

        var symbols = symbolTotals
            .OrderByDescending(x => x.Value)
            .Take(TopSymbolsLimit)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var symbolIndex = symbols
            .Select((symbol, index) => new { symbol, index })
            .ToDictionary(x => x.symbol, x => x.index, StringComparer.Ordinal);

        var matrix = new double[symbols.Length, symbols.Length];
        var totalScore = 0d;

        foreach (var item in positive)
        {
            if (!symbolIndex.TryGetValue(item.ExpectedSymbol, out var rowIndex)
                || !symbolIndex.TryGetValue(item.ActualSymbol, out var columnIndex))
            {
                continue;
            }

            matrix[rowIndex, columnIndex] += item.Score;
            totalScore += item.Score;
        }

        var maxScore = matrix.Cast<double>().DefaultIfEmpty(0).Max();
        if (maxScore <= 0)
        {
            return new ConfusionMatrixResult { Status = ConfusionMatrixStatus.NoVisibleAfterFiltering };
        }

        return new ConfusionMatrixResult
        {
            Status = ConfusionMatrixStatus.Available,
            Matrix = new ConfusionMatrix
            {
                Symbols = symbols,
                Cells = matrix,
                TotalScore = totalScore,
                MaxScore = maxScore,
            },
        };
    }

    public IReadOnlyList<WeightedSymbolCount> WeightedSymbolCounts(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now)
    {
        var counts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in WeightedItems(observations, halfLifeDays, now))
        {
            if (item.Score <= 0)
            {
                continue;
            }

            AddScore(counts, item.ExpectedSymbol, item.Score);
            AddScore(counts, item.ActualSymbol, item.Score);
        }

        return counts.Select(kv => new WeightedSymbolCount { Symbol = kv.Key, Score = kv.Value }).ToArray();
    }

    public string? BuildPracticeConfusionsCharacterSet(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now, int targetSymbolCount)
    {
        var counts = WeightedSymbolCounts(observations, halfLifeDays, now);
        if (counts.Count == 0)
        {
            return null;
        }

        var totalWeight = counts.Sum(x => x.Score);
        var targetSymbols = Math.Max(counts.Count, targetSymbolCount);
        var scaledCounts = counts
            .ToDictionary(
                x => x.Symbol,
                x => Math.Max(1, (int)Math.Round((x.Score / totalWeight) * targetSymbols, MidpointRounding.AwayFromZero)),
                StringComparer.Ordinal);

        var orderedSymbols = scaledCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToArray();

        var characterSet = string.Concat(orderedSymbols.Select(kv => string.Concat(Enumerable.Repeat(kv.Key, kv.Value))));
        return string.IsNullOrWhiteSpace(characterSet) ? null : characterSet;
    }

    private static List<WeightedItem> WeightedItems(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now)
    {
        var items = new List<WeightedItem>();
        foreach (var observation in observations)
        {
            if (string.Equals(observation.ExpectedSymbol, GapSymbol, StringComparison.Ordinal)
                || string.Equals(observation.ActualSymbol, GapSymbol, StringComparison.Ordinal))
            {
                continue;
            }

            items.Add(new WeightedItem
            {
                ExpectedSymbol = observation.ExpectedSymbol,
                ActualSymbol = observation.ActualSymbol,
                Score = CalculateScore(observation, now, halfLifeDays),
            });
        }

        return items;
    }

    private static void AddScore(IDictionary<string, double> counts, string symbol, double score)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.Equals(symbol, GapSymbol, StringComparison.Ordinal))
        {
            return;
        }

        if (counts.TryGetValue(symbol, out var existing))
        {
            counts[symbol] = existing + score;
            return;
        }

        counts[symbol] = score;
    }

    private static double CalculateScore(ConfusionObservation observation, DateTimeOffset now, double halfLifeDays)
    {
        if (observation.Count <= 0)
        {
            return 0;
        }

        var ageDays = Math.Max(0, (now - observation.RecordedAt).TotalDays);
        // Half-life decay: weight halves every halfLifeDays.
        var decay = Math.Pow(2.0, -ageDays / halfLifeDays);
        var distanceFactor = 1d / Math.Max(1, observation.Distance);
        return observation.Count * decay * distanceFactor;
    }

    private sealed record WeightedItem
    {
        public string ExpectedSymbol { get; init; } = string.Empty;
        public string ActualSymbol { get; init; } = string.Empty;
        public double Score { get; init; }
    }
}
