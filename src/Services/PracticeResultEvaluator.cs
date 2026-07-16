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
        var edits = LevenshteinAlignment.Align(expected, actual);
        var tokensReversed = new List<string>();

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
                tokensReversed.Add(edit.Expected.ToString());
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Delete)
            {
                deletedReversed.Append(edit.Expected);
                continue;
            }

            if (edit.Kind == LevenshteinEditKind.Insert)
            {
                insertedReversed.Append(edit.Actual);
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
