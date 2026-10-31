using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The settings of PentaGrammata, a Morse code copying practice application")]
public sealed class AppConfiguration
{
    [PromptuariumComment("The tone, its envelope and the simulated band conditions")]
    public Audio Audio { get; set; } = new();
    [PromptuariumComment("The practice sessions: speed, duration, character set and scoring")]
    public Practice Practice { get; set; } = new();
    [PromptuariumComment("The statistics dialogs")]
    public Analytics Analytics { get; set; } = new();
    [PromptuariumComment("The named character sets: the name, and the characters sent from it (prosigns as <ar>, <as>, <bk>, <bt>, <kn>, <sk>)")]
    public CharacterSets CharacterSets { get; set; } = new();
    [PromptuariumComment("The appearance and behavior of the user interface")]
    public UiPreferences UiPreferences { get; set; } = new();

    /// <summary>
    /// Produces an independent deep copy of the entire configuration graph.
    /// This is the single authority for copying configuration; callers must not
    /// hand-copy individual properties.
    /// </summary>
    public AppConfiguration Clone() => new()
    {
        Audio = (Audio ?? new Audio()).Clone(),
        Practice = (Practice ?? new Practice()).Clone(),
        Analytics = (Analytics ?? new Analytics()).Clone(),
        CharacterSets = (CharacterSets ?? new CharacterSets()).Clone(),
        UiPreferences = (UiPreferences ?? new UiPreferences()).Clone(),
    };
}
