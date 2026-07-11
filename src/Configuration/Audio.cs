namespace PentaGrammata.Configuration;

public sealed class Audio
{
	public int SampleRate { get; set; } = 44100;
	public double Frequency { get; set; } = 523.25;

	/// <summary>
	/// CW signal level in dBFS. 0 dB is full scale; negative values are quieter. This is
	/// the reference level the noise <see cref="NoiseSettings.LevelDb"/> is measured against.
	/// </summary>
	public double VolumeDb { get; set; } = -3.0;
	public int BeepRampMs { get; set; } = 4;
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
