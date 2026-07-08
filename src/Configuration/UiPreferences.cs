using System.Collections.Generic;

namespace PentaGrammata.Configuration;

public sealed class UiPreferences
{
    public List<string> SuppressedDialogs { get; set; } = [];
    public double ReceivedTextFontSize { get; set; } = 24.0;
    public bool RevealSentTextAfterPractice { get; set; } = true;

    public UiPreferences Clone() => new()
    {
        SuppressedDialogs = [.. SuppressedDialogs],
        ReceivedTextFontSize = ReceivedTextFontSize,
        RevealSentTextAfterPractice = RevealSentTextAfterPractice,
    };
}
