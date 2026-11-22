using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

/// <summary>
/// How many times an item of a practice topic is sent in a row: each item is repeated a random
/// number of times between <see cref="Minimum"/> and <see cref="Maximum"/>, both included, as
/// in a real QSO where the name or the QTH is given twice.
/// </summary>
[PromptuariumComment("How many times each item is sent in a row: a random count between Minimum and Maximum")]
public sealed class Repetition
{
	/// <summary>The highest repetition count the settings accept.</summary>
	public const int Limit = 9;

	public Repetition()
	{
	}

	public Repetition(int minimum, int maximum)
	{
		Minimum = minimum;
		Maximum = maximum;
	}

	[PromptuariumComment("The fewest times an item is sent in a row (1 to 9)")]
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
	public int Minimum { get; set; } = 1;

	[PromptuariumComment("The most times an item is sent in a row (Minimum to 9)")]
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
	public int Maximum { get; set; } = 1;

	public Repetition Clone() => new(Minimum, Maximum);
}
