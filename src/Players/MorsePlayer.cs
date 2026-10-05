using System;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Players;

public class MorsePlayer(IAudioPlayer audioPlayer, IMorseSignalRendererFactory rendererFactory, Random? random = null) : IMorsePlayer
{
    private readonly IAudioPlayer _audioPlayer = audioPlayer;
    private readonly IMorseSignalRenderer _renderer = rendererFactory.Create(random ?? new Random());

    public async Task PlayMorseCodeAsync(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken)
    {
        var audioData = await Task.Run(
            () => GenerateAudioData(morseCode, settings, cancellationToken),
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
    private short[] GenerateAudioData(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken)
    {
        var rendered = _renderer.Render(morseCode, settings, cancellationToken);
        var samples = new short[rendered.Length];

        for (int i = 0; i < rendered.Length; i++)
        {
            if ((i & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            samples[i] = (short)Math.Clamp(rendered[i], short.MinValue, short.MaxValue);
        }

        return samples;
    }
}
