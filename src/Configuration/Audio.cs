namespace PentaGrammata.Configuration;

public sealed class Audio
{
	public int SampleRate { get; set; } = 44100;
	public double Frequency { get; set; } = 523.25;
	public double Volume { get; set; } = 0.7;
	public int BeepRampMs { get; set; } = 4;
}
