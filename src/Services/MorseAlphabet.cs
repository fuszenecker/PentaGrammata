using System;
using System.Collections.Generic;
using System.Globalization;

namespace PentaGrammata.Services;

/// <summary>
/// The single authority for which tokens can be sent as Morse code and what elements they
/// map to. Shared by <see cref="MorsePlayer"/> (rendering) and
/// <see cref="PracticeSettingsValidator"/> (rejecting unsendable user-entered custom text)
/// so the two can never disagree about what is playable.
/// </summary>
public static class MorseAlphabet
{
    /// <summary>Dit/dah elements for a single character or prosign token, or "" when unsupported.</summary>
    public static string GetSymbols(string token)
    {
        return token switch
        {
            "a" => ".-",
            "b" => "-...",
            "c" => "-.-.",
            "d" => "-..",
            "e" => ".",
            "f" => "..-.",
            "g" => "--.",
            "h" => "....",
            "i" => "..",
            "j" => ".---",
            "k" => "-.-",
            "l" => ".-..",
            "m" => "--",
            "n" => "-.",
            "o" => "---",
            "p" => ".--.",
            "q" => "--.-",
            "r" => ".-.",
            "s" => "...",
            "t" => "-",
            "u" => "..-",
            "v" => "...-",
            "w" => ".--",
            "x" => "-..-",
            "y" => "-.--",
            "z" => "--..",
            "1" => ".----",
            "2" => "..---",
            "3" => "...--",
            "4" => "....-",
            "5" => ".....",
            "6" => "-....",
            "7" => "--...",
            "8" => "---..",
            "9" => "----.",
            "0" => "-----",
            " " => " ",
            "/" => "-..-.",
            "=" => "-...-",
            "?" => "..--..",
            "+" => ".-.-.",
            "<ar>" => ".-.-.",
            "<as>" => ".-...",
            "<bk>" => "-...-.-",
            "<bt>" => "-...-",
            "<kn>" => "-.--.",
            "<sk>" => "...-.-",
            _ => ""
        };
    }

    /// <summary>Whether <paramref name="token"/> can be sent (case-insensitive).</summary>
    public static bool Supports(string token) =>
        GetSymbols(token.ToLower(CultureInfo.InvariantCulture)).Length > 0;

    /// <summary>
    /// Splits text into sendable tokens: single characters, plus <c>&lt;xx&gt;</c> prosigns kept
    /// whole. An unterminated <c>&lt;</c> is yielded as a lone character so callers can report it.
    /// </summary>
    public static IEnumerable<string> Tokenize(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '<')
            {
                int end = text.IndexOf('>', i);
                if (end > i)
                {
                    yield return text.Substring(i, end - i + 1);
                    i = end;
                    continue;
                }
            }

            yield return c.ToString();
        }
    }
}
