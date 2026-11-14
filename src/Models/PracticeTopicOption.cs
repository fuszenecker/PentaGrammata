using System.Collections.Generic;
using PentaGrammata.Configuration;

namespace PentaGrammata.Models;

/// <summary>A practice topic with the name the topic dropdowns show for it.</summary>
public sealed record PracticeTopicOption(PracticeTopic Topic, string DisplayName)
{
    public static IReadOnlyList<PracticeTopicOption> All { get; } =
    [
        new(PracticeTopic.Groups, "Random 5-character groups"),
        new(PracticeTopic.CallsignsEmea, "Callsigns (EMEA)"),
        new(PracticeTopic.CallsignsGlobal, "Callsigns (global)"),
        new(PracticeTopic.Cities, "Cities"),
        new(PracticeTopic.Names, "Names"),
        new(PracticeTopic.Abbreviations, "Abbreviations & Q-codes"),
        new(PracticeTopic.CustomText, "Custom text"),
        new(PracticeTopic.Mixed, "Mixed"),
    ];

    /// <summary>Whether sessions of <paramref name="topic"/> draw from the selected character set.</summary>
    public static bool UsesCharacterSet(PracticeTopic topic) =>
        topic is PracticeTopic.Groups or PracticeTopic.Mixed;

    /// <summary>Whether the session duration decides how much <paramref name="topic"/> sends.</summary>
    public static bool UsesDuration(PracticeTopic topic) => topic != PracticeTopic.CustomText;

    public override string ToString() => DisplayName;
}
