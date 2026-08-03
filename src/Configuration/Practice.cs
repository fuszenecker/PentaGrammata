namespace PentaGrammata.Configuration;

public sealed class Practice
{
	public int DefaultDurationMins { get; set; } = 5;
	public int CharacterWpm { get; set; } = 20;
	public int AverageWpm { get; set; } = 15;
	public string DefaultCharacterSet { get; set; } = "Default";
	public double ErrorThreshold { get; set; } = 10.0;

	/// <summary>
	/// User-supplied text to send instead of randomly generated 5-character groups. When
	/// blank (the default) groups are generated from the selected character set as usual.
	/// </summary>
	public string CustomText { get; set; } = string.Empty;

	public Practice Clone() => new()
	{
		DefaultDurationMins = DefaultDurationMins,
		CharacterWpm = CharacterWpm,
		AverageWpm = AverageWpm,
		DefaultCharacterSet = DefaultCharacterSet,
		ErrorThreshold = ErrorThreshold,
		CustomText = CustomText,
	};
}
