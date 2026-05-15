using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

public sealed class PracticeResultEvaluator : IPracticeResultEvaluator
{
    public PracticeResult Evaluate(string sentText, string receivedText, double errorThresholdPercent)
    {
        var sentGroups = SplitGroups(sentText ?? string.Empty);
        var receivedGroups = SplitGroups(receivedText ?? string.Empty);

        var rowCount = Math.Max(sentGroups.Count, receivedGroups.Count);
        var rows = new List<PracticeResultRow>(rowCount);

        var characterCount = sentGroups.Sum(x => x.Length);
        var errorCount = 0;

        for (var i = 0; i < rowCount; i++)
        {
            var sent = i < sentGroups.Count ? sentGroups[i] : string.Empty;
            var received = i < receivedGroups.Count ? receivedGroups[i] : string.Empty;

            var groupErrors = GetLevenshteinDistance(sent, received);
            errorCount += groupErrors;

            rows.Add(new PracticeResultRow
            {
                SentGroup = sent,
                ReceivedGroup = received,
                Difference = BuildDifferenceText(sent, received)
            });
        }

        var errorRatePercent = characterCount > 0
            ? (double)errorCount / characterCount * 100d
            : 0d;

        return new PracticeResult
        {
            Rows = rows,
            CharacterCount = characterCount,
            ErrorCount = errorCount,
            ErrorRatePercent = errorRatePercent,
            IsSuccessful = errorRatePercent <= errorThresholdPercent
        };
    }

    private static List<string> SplitGroups(string text)
    {
        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static string BuildDifferenceText(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(Enumerable.Repeat(".", expected.Length));
        }

        var matrix = BuildLevenshteinMatrix(expected, actual);
        var tokensReversed = new List<string>();

        var i = expected.Length;
        var j = actual.Length;

        var insertedReversed = new StringBuilder();
        var deletedReversed = new StringBuilder();

        void FlushEditBuffers()
        {
            if (insertedReversed.Length > 0)
            {
                var inserted = new string(insertedReversed.ToString().Reverse().ToArray());
                tokensReversed.Add($"[+{inserted}]");
                insertedReversed.Clear();
            }

            if (deletedReversed.Length > 0)
            {
                var deleted = new string(deletedReversed.ToString().Reverse().ToArray());
                tokensReversed.Add($"[-{deleted}]");
                deletedReversed.Clear();
            }
        }

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && AreEqualIgnoreCase(expected[i - 1], actual[j - 1]) && matrix[i, j] == matrix[i - 1, j - 1])
            {
                FlushEditBuffers();
                tokensReversed.Add(".");
                i--;
                j--;
                continue;
            }

            if (i > 0 && j > 0 && matrix[i, j] == matrix[i - 1, j - 1] + 1)
            {
                FlushEditBuffers();
                tokensReversed.Add(expected[i - 1].ToString());
                i--;
                j--;
                continue;
            }

            if (i > 0 && matrix[i, j] == matrix[i - 1, j] + 1)
            {
                deletedReversed.Append(expected[i - 1]);
                i--;
                continue;
            }

            if (j > 0 && matrix[i, j] == matrix[i, j - 1] + 1)
            {
                insertedReversed.Append(actual[j - 1]);
                j--;
                continue;
            }

            if (i > 0 && j > 0)
            {
                FlushEditBuffers();
                tokensReversed.Add(expected[i - 1].ToString());
                i--;
                j--;
            }
            else if (i > 0)
            {
                deletedReversed.Append(expected[i - 1]);
                i--;
            }
            else
            {
                insertedReversed.Append(actual[j - 1]);
                j--;
            }
        }

        FlushEditBuffers();
        tokensReversed.Reverse();
        return string.Concat(tokensReversed);
    }

    private static int GetLevenshteinDistance(string expected, string actual)
    {
        var matrix = BuildLevenshteinMatrix(expected, actual);
        return matrix[expected.Length, actual.Length];
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
                    Math.Min(
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
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
