namespace PentaGrammata.Services;

/// <summary>
/// Immutable bundle of the parameters needed to render Morse code to audio.
/// Replaces a long positional parameter list on <see cref="IMorsePlayer"/>.
/// </summary>
public sealed record MorsePlaybackSettings
{
    public required int CharacterWpm { get; init; }
    public required int AverageWpm { get; init; }
    public required int SampleRate { get; init; }
    public required double Frequency { get; init; }
    public required double Volume { get; init; }
    public required int BeepRampMs { get; init; }
}
