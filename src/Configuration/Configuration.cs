namespace PentaGrammata.Configuration;

public sealed class Configuration
{
    public Audio Audio { get; set; } = new();
    public Practice Practice { get; set; } = new();
    public CharacterSets CharacterSets { get; set; } = new();
    public UiPreferences UiPreferences { get; set; } = new();
}
