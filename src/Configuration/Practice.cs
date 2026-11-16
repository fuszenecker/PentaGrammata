using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The practice sessions")]
public sealed class Practice
{
	[PromptuariumComment("The length of a session, in minutes")]
	public int DefaultDurationMins { get; set; } = 5;
	[PromptuariumComment("The character speed, in words per minute (Farnsworth timing)")]
	public int CharacterWpm { get; set; } = 20;
	[PromptuariumComment("The overall speed including the spacing, in words per minute (Farnsworth timing)")]
	public int AverageWpm { get; set; } = 15;
	[PromptuariumComment("The name of the character set the groups are drawn from (see CharacterSets)")]
	public string DefaultCharacterSet { get; set; } = "Default";
	[PromptuariumComment("What a session sends: Groups, CallsignsEmea, CallsignsGlobal, Cities, Names, Abbreviations, CustomText or Mixed")]
	public PracticeTopic Topic { get; set; } = PracticeTopic.Groups;
	[PromptuariumComment("The highest error rate, in percent, at which a session counts as successful")]
	public double ErrorThreshold { get; set; } = 10.0;

	/// <summary>
	/// The user's own text, sent when <see cref="Topic"/> is <see cref="PracticeTopic.CustomText"/>
	/// (and mixed in word by word by <see cref="PracticeTopic.Mixed"/>).
	/// </summary>
	[PromptuariumComment("The text the CustomText topic sends (and the Mixed topic mixes in, word by word)")]
	public string CustomText { get; set; } = string.Empty;

	/// <summary>
	/// When true, the CustomText topic sends the words of <see cref="CustomText"/> in a random
	/// order instead of as written.
	/// </summary>
	[PromptuariumComment("Whether the CustomText topic shuffles the words instead of sending them in order")]
	public bool ShuffleCustomText { get; set; }

	/// <summary>
	/// When true, the practice WPM is adjusted in memory after each scored session: it slows
	/// down when the recent average error rate — or the session that just finished — is above
	/// the expected error rate, and speeds up otherwise. The dynamic WPM itself is never
	/// persisted; it restarts from the configured WPM on every app start.
	/// </summary>
	[PromptuariumComment("Whether the average speed adapts after each session (in memory only, restarts from the configured speed)")]
	public bool AutoAdjustWpm { get; set; }

	/// <summary>
	/// Number of most recent sessions whose error rates are averaged to drive
	/// <see cref="AutoAdjustWpm"/>. Must be at least 1.
	/// </summary>
	[PromptuariumComment("The number of recent sessions whose error rates drive the automatic speed adjustment")]
	public int AutoAdjustWindowSize { get; set; } = 1;

	public Practice Clone() => new()
	{
		DefaultDurationMins = DefaultDurationMins,
		CharacterWpm = CharacterWpm,
		AverageWpm = AverageWpm,
		DefaultCharacterSet = DefaultCharacterSet,
		Topic = Topic,
		ErrorThreshold = ErrorThreshold,
		CustomText = CustomText,
		ShuffleCustomText = ShuffleCustomText,
		AutoAdjustWpm = AutoAdjustWpm,
		AutoAdjustWindowSize = AutoAdjustWindowSize,
	};
}
