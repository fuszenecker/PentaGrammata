namespace PentaGrammata.Configuration;

public sealed class Practice
{
	public int DefaultDurationMins { get; set; } = 5;
	public int CharacterWpm { get; set; } = 20;
	public int AverageWpm { get; set; } = 15;
	public string DefaultCharacterSet { get; set; } = "Default";
}
