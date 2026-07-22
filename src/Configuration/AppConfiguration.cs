namespace PentaGrammata.Configuration;

public sealed class AppConfiguration
{
    public Audio Audio { get; set; } = new();
    public Practice Practice { get; set; } = new();
    public Analytics Analytics { get; set; } = new();
    public CharacterSets CharacterSets { get; set; } = new();
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
