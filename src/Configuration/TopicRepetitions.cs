using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// The <see cref="Repetition"/> of every practice topic that sends items. The random groups
/// are never repeated; the mixed topic repeats each item as its own topic would.
/// </summary>
[PromptuariumComment("How many times each item of a topic is sent in a row (groups are never repeated)")]
public sealed class TopicRepetitions
{
	[PromptuariumComment("The repetition of the EMEA callsigns")]
	public Repetition CallsignsEmea { get; set; } = new(2, 2);

	[PromptuariumComment("The repetition of the worldwide callsigns")]
	public Repetition CallsignsGlobal { get; set; } = new(2, 2);

	[PromptuariumComment("The repetition of the cities")]
	public Repetition Cities { get; set; } = new(2, 2);

	[PromptuariumComment("The repetition of the names")]
	public Repetition Names { get; set; } = new(2, 2);

	[PromptuariumComment("The repetition of the abbreviations")]
	public Repetition Abbreviations { get; set; } = new(1, 1);

	[PromptuariumComment("The repetition of each word of the custom text")]
	public Repetition CustomText { get; set; } = new(1, 1);

	public TopicRepetitions Clone() => new()
	{
		CallsignsEmea = (CallsignsEmea ?? new(2, 2)).Clone(),
		CallsignsGlobal = (CallsignsGlobal ?? new(2, 2)).Clone(),
		Cities = (Cities ?? new(2, 2)).Clone(),
		Names = (Names ?? new(2, 2)).Clone(),
		Abbreviations = (Abbreviations ?? new(1, 1)).Clone(),
		CustomText = (CustomText ?? new(1, 1)).Clone(),
	};
}
