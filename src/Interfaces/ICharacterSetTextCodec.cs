using System.Collections.Generic;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Encodes character sets between the strongly-typed configuration dictionary and
/// the multi-line editor text ("Name = Value" lines) shown in the settings dialog.
/// </summary>
public interface ICharacterSetTextCodec
{
    string FormatForEditor(IReadOnlyDictionary<string, string> characterSets);

    bool TryParse(string text, out Dictionary<string, string> parsedSets, out string error);
}
