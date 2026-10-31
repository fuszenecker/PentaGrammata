using System.Collections.Generic;

using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The appearance and behavior of the user interface")]
public sealed class UiPreferences
{
    [PromptuariumComment("The dialogs the user asked not to show again")]
    public List<string> SuppressedDialogs { get; set; } = [];
    [PromptuariumComment("The font of the text box the received characters are typed into")]
    public string ReceivedTextFontFamily { get; set; } = "Cascadia Mono";
    [PromptuariumComment("The font size of the received text box, in points")]
    public double ReceivedTextFontSize { get; set; } = 20.0;
    [PromptuariumComment("Whether the sent text is shown after a session")]
    public bool RevealSentTextAfterPractice { get; set; } = true;
    [PromptuariumComment("Whether the revealed sent text is shown in lowercase")]
    public bool RevealSentTextInLowercase { get; set; } = false;

    public UiPreferences Clone() => new()
    {
        SuppressedDialogs = [.. SuppressedDialogs],
        ReceivedTextFontFamily = ReceivedTextFontFamily,
        ReceivedTextFontSize = ReceivedTextFontSize,
        RevealSentTextAfterPractice = RevealSentTextAfterPractice,
        RevealSentTextInLowercase = RevealSentTextInLowercase,
    };
}
