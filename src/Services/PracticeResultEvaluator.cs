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
        // The sent text keeps a multi-word item (its words joined by an underscore) as one
        // group; the copy is typed with ordinary spaces, so its words are grouped the same way:
        // a sent item of k words takes the next k received words. Within a group the words are
        // compared one by one, so scoring is the same as word by word.
        var sentGroups = PracticeItems.SplitItems(sentText);
        var receivedWords = PracticeItems.SplitWords(receivedText);

        var rows = new List<PracticeResultRow>();
        var characterCount = 0;
        var errorCount = 0;
        var nextReceived = 0;

        foreach (var sent in sentGroups)
        {
            var sentWords = PracticeItems.Words(sent);
            var receivedForGroup = receivedWords.Skip(nextReceived).Take(sentWords.Length).ToList();
            nextReceived += receivedForGroup.Count;
            rows.Add(BuildRow(sentWords, receivedForGroup, ref characterCount, ref errorCount));
        }

        // Words typed beyond the sent text are one group each, as before.
        foreach (var extra in receivedWords.Skip(nextReceived))
        {
            rows.Add(BuildRow([], [extra], ref characterCount, ref errorCount));
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

    private static PracticeResultRow BuildRow(
        IReadOnlyList<string> sentWords,
        IReadOnlyList<string> receivedWords,
        ref int characterCount,
        ref int errorCount)
    {
        var wordCount = Math.Max(sentWords.Count, receivedWords.Count);
        var differences = new List<string>(wordCount);
        for (var i = 0; i < wordCount; i++)
        {
            var sent = i < sentWords.Count ? sentWords[i] : string.Empty;
            var received = i < receivedWords.Count ? receivedWords[i] : string.Empty;
            characterCount += LevenshteinAlignment.CountSymbols(sent);
            errorCount += GetLevenshteinDistance(sent, received);
            differences.Add(BuildDifferenceText(sent, received));
        }

        return new PracticeResultRow
        {
            SentGroup = string.Join(PracticeItems.WordJoiner, sentWords),
            ReceivedGroup = string.Join(PracticeItems.WordJoiner, receivedWords),
            Difference = string.Join(' ', differences),
        };
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
