using System;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

public class MorsePlayer(IAudioPlayer audioPlayer, INoiseGeneratorFactory noiseGeneratorFactory, Random? random = null) : IMorsePlayer
{
    private readonly IAudioPlayer _audioPlayer = audioPlayer;
    private readonly MorseSignalRenderer _renderer = new(noiseGeneratorFactory, random ?? new Random());

    public async Task PlayMorseCodeAsync(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken)
    {
        var audioData = await Task.Run(
            () => GenerateAudioData(morseCode, settings),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await _audioPlayer.PlayAudioAsync(audioData, settings.SampleRate, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Renders the message and folds it into 16-bit samples. The receiver chain can overshoot
    /// full scale (AGC boost, APF blend), and anything above it is hard-clipped here — which is
    /// audible distortion, so the settings dialog warns about it up front via
    /// <see cref="Interfaces.IAudioHeadroomAnalyzer"/>.
    /// </summary>
    private short[] GenerateAudioData(string morseCode, MorsePlaybackSettings settings)
    {
        var rendered = _renderer.Render(morseCode, settings);
        var samples = new short[rendered.Length];

        for (int i = 0; i < rendered.Length; i++)
        {
            samples[i] = (short)Math.Clamp(rendered[i], short.MinValue, short.MaxValue);
        }

        return samples;
    }
}
