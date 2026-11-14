using System.Collections.Generic;
using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// The word lists the practice topics draw from (see <see cref="PracticeTopic"/>). The
/// defaults live in the bundled <c>settings.prmt</c>; a saved list replaces the bundled one as
/// a whole.
/// </summary>
[PromptuariumComment("The word lists of the practice topics")]
public sealed class Topics
{
    [PromptuariumComment("Callsign patterns of the EMEA region: # is a random digit, a lowercase letter a random letter, uppercase letters, digits and / are sent as written")]
    public List<string> CallsignPatternsEmea { get; set; } = [];

    [PromptuariumComment("Callsign patterns of the whole world: # is a random digit, a lowercase letter a random letter, uppercase letters, digits and / are sent as written")]
    public List<string> CallsignPatternsGlobal { get; set; } = [];

    [PromptuariumComment("City names: the larger cities of the EMEA region and the capitals of the world")]
    public List<string> Cities { get; set; } = [];

    [PromptuariumComment("First names popular in Europe, and the nicknames radio amateurs use on the air")]
    public List<string> Names { get; set; } = [];

    [PromptuariumComment("Radio amateur abbreviations and Q-codes")]
    public List<string> Abbreviations { get; set; } = [];

    public Topics Clone() => new()
    {
        CallsignPatternsEmea = [.. CallsignPatternsEmea ?? []],
        CallsignPatternsGlobal = [.. CallsignPatternsGlobal ?? []],
        Cities = [.. Cities ?? []],
        Names = [.. Names ?? []],
        Abbreviations = [.. Abbreviations ?? []],
    };
}
