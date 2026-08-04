using System.Text;

namespace PentaGrammata.Services;

/// <summary>
/// Turns the user's custom practice text into the form the Morse player consumes. The stored
/// setting keeps whatever the user typed (including line breaks, so the settings dialog can
/// show it back unchanged); every consumer normalizes through here so a newline or a run of
/// spaces means exactly one word gap. Both the validator and the practice controller use it,
/// so what gets validated is what gets sent.
/// </summary>
public static class CustomTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder(text.Length);
        bool pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(c);
        }

        return result.ToString();
    }
}
