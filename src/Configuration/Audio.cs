using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The synthesized Morse audio")]
public sealed class Audio
{
	[PromptuariumComment("The sample rate of the audio output, in Hz")]
	public int SampleRate { get; set; } = 44100;
	[PromptuariumComment("The tone frequency, in Hz")]
	public double Frequency { get; set; } = 523.25;

	/// <summary>
	/// CW signal level in dBFS. 0 dB is full scale; negative values are quieter. This is
	/// the reference level the noise <see cref="NoiseSettings.LevelDb"/> is measured against.
	/// </summary>
	[PromptuariumComment("The signal level, in dBFS (0 is full scale, negative is quieter); the noise level is relative to it")]
	public double VolumeDb { get; set; } = -3.0;
	[PromptuariumComment("The rise and fall time of each element's envelope, in milliseconds (against key clicks)")]
	public int BeepRampMs { get; set; } = 4;
	[PromptuariumComment("The simulated receiver: background noise, filters, AGC and fading (QSB)")]
	public NoiseSettings Noise { get; set; } = new();

	public Audio Clone() => new()
	{
		SampleRate = SampleRate,
		Frequency = Frequency,
		VolumeDb = VolumeDb,
		BeepRampMs = BeepRampMs,
		Noise = (Noise ?? new NoiseSettings()).Clone(),
	};
}
