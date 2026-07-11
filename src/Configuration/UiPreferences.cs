using System.Collections.Generic;

namespace PentaGrammata.Configuration;

public sealed class UiPreferences
{
    public List<string> SuppressedDialogs { get; set; } = [];
    public string ReceivedTextFontFamily { get; set; } = "Cascadia Mono";
    public double ReceivedTextFontSize { get; set; } = 20.0;
    public bool RevealSentTextAfterPractice { get; set; } = true;

    public UiPreferences Clone() => new()
    {
        SuppressedDialogs = [.. SuppressedDialogs],
        ReceivedTextFontFamily = ReceivedTextFontFamily,
        ReceivedTextFontSize = ReceivedTextFontSize,
        RevealSentTextAfterPractice = RevealSentTextAfterPractice,
    };
}
