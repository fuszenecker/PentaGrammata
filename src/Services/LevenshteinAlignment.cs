using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PentaGrammata.Services;

public enum LevenshteinEditKind
{
    Match,
    Substitute,
    Delete,
    Insert
}

public readonly record struct LevenshteinEdit(LevenshteinEditKind Kind, string Expected, string Actual);

public static class LevenshteinAlignment
{
    public static int GetDistance(string expected, string actual)
    {
        var expectedSymbols = TokenizeSymbols(expected);
        var actualSymbols = TokenizeSymbols(actual);
        var matrix = BuildLevenshteinMatrix(expectedSymbols, actualSymbols);
        return matrix[expectedSymbols.Count, actualSymbols.Count];
    }

    public static IReadOnlyList<LevenshteinEdit> Align(string expected, string actual)
    {
        var expectedSymbols = TokenizeSymbols(expected);
        var actualSymbols = TokenizeSymbols(actual);
        var matrix = BuildLevenshteinMatrix(expectedSymbols, actualSymbols);
        var editsReversed = new List<LevenshteinEdit>(Math.Max(expectedSymbols.Count, actualSymbols.Count));

        var i = expectedSymbols.Count;
        var j = actualSymbols.Count;

        while (i > 0 || j > 0)
        {
            if (i > 0
                && j > 0
                && AreEqualIgnoreCase(expectedSymbols[i - 1], actualSymbols[j - 1])
                && matrix[i, j] == matrix[i - 1, j - 1])
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Match, expectedSymbols[i - 1], actualSymbols[j - 1]));
                i--;
                j--;
                continue;
            }

            if (i > 0 && j > 0 && matrix[i, j] == matrix[i - 1, j - 1] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Substitute, expectedSymbols[i - 1], actualSymbols[j - 1]));
                i--;
                j--;
                continue;
            }

            if (i > 0 && matrix[i, j] == matrix[i - 1, j] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Delete, expectedSymbols[i - 1], string.Empty));
                i--;
                continue;
            }

            if (j > 0 && matrix[i, j] == matrix[i, j - 1] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Insert, string.Empty, actualSymbols[j - 1]));
                j--;
                continue;
            }

            if (i > 0 && j > 0)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Substitute, expectedSymbols[i - 1], actualSymbols[j - 1]));
                i--;
                j--;
            }
            else if (i > 0)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Delete, expectedSymbols[i - 1], string.Empty));
                i--;
            }
            else
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Insert, string.Empty, actualSymbols[j - 1]));
                j--;
            }
        }

        editsReversed.Reverse();
        return editsReversed;
    }

    private static int[,] BuildLevenshteinMatrix(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var rows = expected.Count + 1;
        var columns = actual.Count + 1;
        var matrix = new int[rows, columns];

        for (var i = 0; i < rows; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j < columns; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < columns; j++)
            {
                var substitutionCost = AreEqualIgnoreCase(expected[i - 1], actual[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + substitutionCost);
            }
        }

        return matrix;
    }

    private static bool AreEqualIgnoreCase(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> TokenizeSymbols(string text)
    {
        var symbols = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return symbols;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '<')
            {
                var endIndex = text.IndexOf('>', i);
                if (endIndex > i)
                {
                    symbols.Add(text.Substring(i, endIndex - i + 1));
                    i = endIndex;
                    continue;
                }
            }

            symbols.Add(char.ToUpperInvariant(c).ToString(CultureInfo.InvariantCulture));
        }

        return symbols;
    }
}
