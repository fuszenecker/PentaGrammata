using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NAudio.Wave;

namespace PentaGrammata.Services;

public class WindowsAudioPlayer : IAudioPlayer
{
    public async Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        if (audioData == null || audioData.Length == 0)
        {
            return;
        }

        try
        {
            var bytes = new byte[audioData.Length * 2];
            Buffer.BlockCopy(audioData, 0, bytes, 0, bytes.Length);

            using var ms = new MemoryStream(bytes);
            using var waveStream = new RawSourceWaveStream(ms, new WaveFormat(sampleRate, 16, 1));
            using var waveOut = new WaveOutEvent();

            waveOut.Init(waveStream);
            waveOut.Play();

            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    waveOut.Stop();
                    break;
                }

                await Task.Delay(10, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when the user stops playback.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing audio: {ex.Message}");
        }
    }
}
