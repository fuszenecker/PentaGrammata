using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The practice sessions")]
public sealed class Practice
{
	[PromptuariumComment("The length of a session, in minutes")]
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
	public int DefaultDurationMins { get; set; } = 5;
	[PromptuariumComment("The character speed, in words per minute (Farnsworth timing)")]
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
	public int CharacterWpm { get; set; } = 20;
	[PromptuariumComment("The overall speed including the spacing, in words per minute (Farnsworth timing)")]
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
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
	[PromptuariumComment("The text the CustomText topic sends, each line one item (the Mixed topic mixes its lines in too)")]
	public string CustomText { get; set; } = string.Empty;

	/// <summary>
	/// When true, the CustomText topic sends the lines of <see cref="CustomText"/> in a random
	/// order instead of as written.
	/// </summary>
	[PromptuariumComment("Whether the CustomText topic shuffles its lines instead of sending them in order")]
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
	[PromptuariumType(PromptuariumType.Float64)] // the form of version 1 (PRMT:CFG1-LE)
	public int AutoAdjustWindowSize { get; set; } = 1;

	/// <summary>
	/// When true, the signal-to-noise ratio is adjusted in memory after each saved session, by
	/// the same decision as <see cref="AutoAdjustWpm"/>: a pass lowers it by
	/// <see cref="AutoAdjustSnrStepDb"/> (more noise), a failure raises it. Only while noise is on;
	/// never persisted, restarts from the configured level like the dynamic WPM.
	/// </summary>
	[PromptuariumComment("Whether the signal-to-noise ratio adapts after each session while noise is on (in memory only, restarts from the configured level)")]
	public bool AutoAdjustSnr { get; set; }

	/// <summary>How much the signal-to-noise ratio moves per adjustment, in dB.</summary>
	[PromptuariumComment("How much the automatic adjustment lowers the signal-to-noise ratio after a passed session and raises it after a failed one, in dB")]
	public double AutoAdjustSnrStepDb { get; set; } = 3.0;

	/// <summary>
	/// When true, the QSB fade depth is adjusted in memory after each saved session, by the same
	/// decision as <see cref="AutoAdjustWpm"/>: a pass deepens it by
	/// <see cref="AutoAdjustQsbStepDb"/>, a failure makes it shallower. Only while QSB is on;
	/// never persisted, restarts from the configured depth like the dynamic WPM.
	/// </summary>
	[PromptuariumComment("Whether the QSB fade depth adapts after each session while QSB is on (in memory only, restarts from the configured depth)")]
	public bool AutoAdjustQsb { get; set; }

	/// <summary>How much the QSB fade depth moves per adjustment, in dB.</summary>
	[PromptuariumComment("How much the automatic adjustment deepens the QSB fade after a passed session and makes it shallower after a failed one, in dB")]
	public double AutoAdjustQsbStepDb { get; set; } = 3.0;

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
		AutoAdjustSnr = AutoAdjustSnr,
		AutoAdjustSnrStepDb = AutoAdjustSnrStepDb,
		AutoAdjustQsb = AutoAdjustQsb,
		AutoAdjustQsbStepDb = AutoAdjustQsbStepDb,
	};
}
