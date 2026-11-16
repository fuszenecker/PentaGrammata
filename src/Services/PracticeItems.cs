using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PentaGrammata.Services;

/// <summary>
/// The items of a practice text. A list entry may hold several words ("NEW YORK", "LITTLE
/// JOHN", "QSL VIA BURO"); in the generated text its words are joined by an underscore, so
/// the result window can tell one multi-word item from separate words. The underscore cannot
/// be sent as Morse code, so it never clashes with a real character. The settings, the user's
/// copy and everything shown for typing use ordinary spaces; an underscore typed anywhere
/// counts as a space, so both mean the same everywhere except in that grouping.
/// </summary>
public static class PracticeItems
{
    /// <summary>The underscore that joins the words of one item.</summary>
    public const char WordJoiner = '_';

    /// <summary>
    /// The item form of a list entry: whitespace (and underscores) collapsed, words joined by
    /// <see cref="WordJoiner"/>.
    /// </summary>
    public static string Join(string? entry) => NormalizeEntry(entry).Replace(' ', WordJoiner);

    /// <summary>
    /// A list entry as the settings keep it: every run of whitespace or underscores becomes
    /// one ordinary space.
    /// </summary>
    public static string NormalizeEntry(string? entry) =>
        CustomTextNormalizer.Normalize(entry?.Replace(WordJoiner, ' '));

    /// <summary>The words of one item.</summary>
    public static string[] Words(string item) => item.Split(WordJoiner, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Splits a practice text into items: at whitespace, keeping <see cref="WordJoiner"/> inside an item.</summary>
    public static List<string> SplitItems(string? text)
    {
        var items = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text ?? string.Empty)
        {
            if (char.IsWhiteSpace(c))
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return items;

        void Flush()
        {
            var item = current.ToString().Trim(WordJoiner);
            if (item.Length > 0)
            {
                items.Add(item);
            }

            current.Clear();
        }
    }

    /// <summary>Splits text into words at every whitespace and at <see cref="WordJoiner"/>.</summary>
    public static List<string> SplitWords(string? text) =>
        PracticeItems.SplitItems(text).SelectMany(Words).ToList();

    /// <summary>
    /// An item as the result window shows it: the joiner becomes a no-break space, which looks
    /// like an ordinary space but keeps a multi-word item on one line.
    /// </summary>
    public static string ToDisplay(string item) => item.Replace(WordJoiner, '\u00A0');

    /// <summary>The text with ordinary spaces only: for playback and for showing it to type.</summary>
    public static string ToPlain(string text) => text.Replace(WordJoiner, ' ');
}
