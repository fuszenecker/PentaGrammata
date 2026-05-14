using System.Collections.Generic;

namespace PentaGrammata.Configuration;

public sealed class PracticeSettings
{
    public int DefaultDurationMins { get; set; }
    public int CharacterWpm { get; set; }
    public int AverageWpm { get; set; }
    public int SampleRate { get; set; }
    public int BeepRampMs { get; set; }
    public string DefaultCharacterSet { get; set; } = "Default";
    public Dictionary<string, string> CharacterSets { get; set; } = new();
}
