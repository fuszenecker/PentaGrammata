namespace PentaGrammata.Configuration;

/// <summary>
/// What a practice session sends. Persisted by name in <c>settings.prmt</c>
/// (<see cref="Practice.Topic"/>), so the member names are part of the file format: do not
/// rename them.
/// </summary>
public enum PracticeTopic
{
    /// <summary>Random 5-character groups drawn from the selected character set.</summary>
    Groups,
    /// <summary>Callsigns generated from the EMEA patterns (<see cref="Topics.CallsignPatternsEmea"/>).</summary>
    CallsignsEmea,
    /// <summary>Callsigns generated from the worldwide patterns (<see cref="Topics.CallsignPatternsGlobal"/>).</summary>
    CallsignsGlobal,
    /// <summary>Words picked from <see cref="Topics.Cities"/>.</summary>
    Cities,
    /// <summary>Words picked from <see cref="Topics.Names"/>.</summary>
    Names,
    /// <summary>Words picked from <see cref="Topics.Abbreviations"/> (ham abbreviations and Q-codes).</summary>
    Abbreviations,
    /// <summary>The user's own text (<see cref="Practice.CustomText"/>), in order or shuffled.</summary>
    CustomText,
    /// <summary>Every other topic mixed, item by item.</summary>
    Mixed,
}
