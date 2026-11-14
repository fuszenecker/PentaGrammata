using System;
using System.Collections.Generic;
using System.Linq;

namespace PentaGrammata.Services;

/// <summary>
/// The editor form of a topic word list: one entry per line. Blank lines are dropped, each
/// entry is trimmed, repeats are dropped (ignoring case), and the lists are kept in alphabetical
/// order, so what the settings dialog saves is exactly what the generators use.
/// </summary>
public static class TopicListText
{
    public static string Format(IEnumerable<string>? items) =>
        string.Join(Environment.NewLine, Sorted(items ?? []));

    public static List<string> Parse(string? text) =>
        Sorted((text ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0));

    /// <summary>Alphabetical order (ignoring case), without repeats.</summary>
    public static List<string> Sorted(IEnumerable<string> items) =>
        items
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item, StringComparer.Ordinal)
            .ToList();
}
