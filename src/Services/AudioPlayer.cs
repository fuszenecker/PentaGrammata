using System;
using System.Threading;
using System.Threading.Tasks;

using ManagedBass;

namespace PentaGrammata.Services;

public class AudioPlayer : IAudioPlayer
{
    public AudioPlayer()
    {
        if (!Bass.Init())
            throw new Exception($"Failed to initialize BASS: {Bass.LastError}");
    }

    public async Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        if (audioData == null || audioData.Length == 0)
            return;

        try
        {
            int handle = Bass.CreateSample(audioData.Length * 2, sampleRate, 1, 1, BassFlags.Default | BassFlags.Mono);

            if (handle == 0)
                throw new Exception($"Failed to create sample: {Bass.LastError}");

            try
            {
                Bass.SampleSetData(handle, audioData);

                int channel = Bass.SampleGetChannel(handle, BassFlags.SampleChannelStream);
                if (channel == 0)
                    throw new Exception($"Failed to get sample channel: {Bass.LastError}");

                Bass.ChannelPlay(channel);

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Bass.ChannelStop(channel);
                        break;
                    }

                    var state = Bass.ChannelIsActive(channel);
                    if (state != PlaybackState.Playing)
                        break;

                    await Task.Delay(10, cancellationToken);
                }
            }
            finally
            {
                Bass.SampleFree(handle);
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