using System;
using System.Collections.Generic;

namespace PentaGrammata.Services;

public enum LevenshteinEditKind
{
    Match,
    Substitute,
    Delete,
    Insert
}

public readonly record struct LevenshteinEdit(LevenshteinEditKind Kind, char Expected, char Actual);

public static class LevenshteinAlignment
{
    public static int GetDistance(string expected, string actual)
    {
        var matrix = BuildLevenshteinMatrix(expected, actual);
        return matrix[expected.Length, actual.Length];
    }

    public static IReadOnlyList<LevenshteinEdit> Align(string expected, string actual)
    {
        var matrix = BuildLevenshteinMatrix(expected, actual);
        var editsReversed = new List<LevenshteinEdit>(Math.Max(expected.Length, actual.Length));

        var i = expected.Length;
        var j = actual.Length;

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && AreEqualIgnoreCase(expected[i - 1], actual[j - 1]) && matrix[i, j] == matrix[i - 1, j - 1])
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Match, expected[i - 1], actual[j - 1]));
                i--;
                j--;
                continue;
            }

            if (i > 0 && j > 0 && matrix[i, j] == matrix[i - 1, j - 1] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Substitute, expected[i - 1], actual[j - 1]));
                i--;
                j--;
                continue;
            }

            if (i > 0 && matrix[i, j] == matrix[i - 1, j] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Delete, expected[i - 1], '\0'));
                i--;
                continue;
            }

            if (j > 0 && matrix[i, j] == matrix[i, j - 1] + 1)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Insert, '\0', actual[j - 1]));
                j--;
                continue;
            }

            if (i > 0 && j > 0)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Substitute, expected[i - 1], actual[j - 1]));
                i--;
                j--;
            }
            else if (i > 0)
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Delete, expected[i - 1], '\0'));
                i--;
            }
            else
            {
                editsReversed.Add(new LevenshteinEdit(LevenshteinEditKind.Insert, '\0', actual[j - 1]));
                j--;
            }
        }

        editsReversed.Reverse();
        return editsReversed;
    }

    private static int[,] BuildLevenshteinMatrix(string expected, string actual)
    {
        var rows = expected.Length + 1;
        var columns = actual.Length + 1;
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

    private static bool AreEqualIgnoreCase(char left, char right)
    {
        return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
    }
}
