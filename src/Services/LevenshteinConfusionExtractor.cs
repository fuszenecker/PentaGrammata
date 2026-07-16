using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using PentaGrammata.Models;

namespace PentaGrammata.Services;

public static class LevenshteinConfusionExtractor
{
    private const string GapSymbol = "_";

    public static IReadOnlyList<ConfusionObservation> Extract(IReadOnlyList<PracticeResultRow> rows, DateTimeOffset recordedAt)
    {
        var counts = new Dictionary<(string Expected, string Actual, int Distance), int>(StringTupleComparer.Ordinal);

        foreach (var row in rows)
        {
            AddFromPair(row.SentGroup, row.ReceivedGroup, counts);
        }

        return counts
            .Select(kvp => new ConfusionObservation
            {
                RecordedAt = recordedAt,
                ExpectedSymbol = kvp.Key.Expected,
                ActualSymbol = kvp.Key.Actual,
                Distance = kvp.Key.Distance,
                Count = kvp.Value
            })
            .ToArray();
    }

    private static void AddFromPair(
        string expected,
        string actual,
        IDictionary<(string Expected, string Actual, int Distance), int> counts)
    {
        var edits = LevenshteinAlignment.Align(expected, actual);
        foreach (var edit in edits)
        {
            if (edit.Kind == LevenshteinEditKind.Match)
            {
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Substitute)
            {
                AddCount(
                    counts,
                    NormalizeSymbol(edit.Expected),
                    NormalizeSymbol(edit.Actual),
                    distance: 1,
                    increment: 1);
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Delete)
            {
                AddCount(
                    counts,
                    NormalizeSymbol(edit.Expected),
                    GapSymbol,
                    distance: 1,
                    increment: 1);
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Insert)
            {
                AddCount(
                    counts,
                    GapSymbol,
                    NormalizeSymbol(edit.Actual),
                    distance: 1,
                    increment: 1);
            }
        }
    }

    private static void AddCount(
        IDictionary<(string Expected, string Actual, int Distance), int> counts,
        string expected,
        string actual,
        int distance,
        int increment)
    {
        var key = (expected, actual, distance);
        if (counts.TryGetValue(key, out var current))
        {
            counts[key] = current + increment;
            return;
        }

        counts[key] = increment;
    }

    private static string NormalizeSymbol(char value)
    {
        return char.ToUpperInvariant(value).ToString(CultureInfo.InvariantCulture);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Expected, string Actual, int Distance)>
    {
        public static readonly StringTupleComparer Ordinal = new();

        public bool Equals((string Expected, string Actual, int Distance) x, (string Expected, string Actual, int Distance) y)
        {
            return string.Equals(x.Expected, y.Expected, StringComparison.Ordinal)
                && string.Equals(x.Actual, y.Actual, StringComparison.Ordinal)
                && x.Distance == y.Distance;
        }

        public int GetHashCode((string Expected, string Actual, int Distance) obj)
        {
            return HashCode.Combine(obj.Expected, obj.Actual, obj.Distance);
        }
    }
}
