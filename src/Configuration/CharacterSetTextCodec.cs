using System;
using System.Collections.Generic;
using System.Linq;

namespace PentaGrammata.Configuration;

public static class CharacterSetTextCodec
{
    public static string FormatForEditor(IReadOnlyDictionary<string, string> characterSets)
    {
        return string.Join(Environment.NewLine,
            characterSets
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => $"{kv.Key} = {kv.Value}"));
    }

    public static bool TryParse(string text, out Dictionary<string, string> parsedSets, out string error)
    {
        parsedSets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
            {
                error = "Character set lines must use Name = Value format.";
                return false;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0)
            {
                error = "Character set name and value cannot be empty.";
                return false;
            }

            parsedSets[name] = value;
        }

        if (parsedSets.Count == 0)
        {
            error = "At least one character set is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}