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

	/// <summary>
	/// When true, the practice WPM is adjusted in memory after each scored session: it slows
	/// down when the recent average error rate — or the session that just finished — is above
	/// the expected error rate, and speeds up otherwise. The dynamic WPM itself is never
	/// persisted; it restarts from the configured WPM on every app start.
	/// </summary>
	public bool AutoAdjustWpm { get; set; }

	/// <summary>
	/// Number of most recent sessions whose error rates are averaged to drive
	/// <see cref="AutoAdjustWpm"/>. Must be at least 1.
	/// </summary>
	public int AutoAdjustWindowSize { get; set; } = 3;

	public Practice Clone() => new()
	{
		DefaultDurationMins = DefaultDurationMins,
		CharacterWpm = CharacterWpm,
		AverageWpm = AverageWpm,
		DefaultCharacterSet = DefaultCharacterSet,
		ErrorThreshold = ErrorThreshold,
		CustomText = CustomText,
		AutoAdjustWpm = AutoAdjustWpm,
		AutoAdjustWindowSize = AutoAdjustWindowSize,
	};
}
