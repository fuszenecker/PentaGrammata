using System;
using System.Collections.Generic;
using System.Linq;
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
        var edits = LevenshteinAlignment.Align(expected, actual);
        var tokensReversed = new List<string>();

        var insertedReversed = new List<string>();
        var deletedReversed = new List<string>();

        void FlushEditBuffers()
        {
            if (insertedReversed.Count > 0)
            {
                insertedReversed.Reverse();
                var inserted = string.Concat(insertedReversed);
                tokensReversed.Add($"[+{inserted}]");
                insertedReversed.Clear();
            }

            if (deletedReversed.Count > 0)
            {
                deletedReversed.Reverse();
                var deleted = string.Concat(deletedReversed);
                tokensReversed.Add($"[-{deleted}]");
                deletedReversed.Clear();
            }
        }

        for (var editIndex = edits.Count - 1; editIndex >= 0; editIndex--)
        {
            var edit = edits[editIndex];

            if (edit.Kind == LevenshteinEditKind.Match)
            {
                FlushEditBuffers();
                tokensReversed.Add(".");
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Substitute)
            {
                FlushEditBuffers();
                // Bracketed so a substituted "." can never be mistaken for the "." match
                // marker, and so prosigns such as <bk> survive as one token.
                tokensReversed.Add($"[!{edit.Expected}]");
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Delete)
            {
                deletedReversed.Add(edit.Expected);
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Insert)
            {
                insertedReversed.Add(edit.Actual);
            }
        }

        FlushEditBuffers();
        tokensReversed.Reverse();
        return string.Concat(tokensReversed);
    }

    private static int GetLevenshteinDistance(string expected, string actual)
    {
        return LevenshteinAlignment.GetDistance(expected, actual);
    }
}
