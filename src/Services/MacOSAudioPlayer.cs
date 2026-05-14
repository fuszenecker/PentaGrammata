using System;
using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

/// <summary>
/// Placeholder macOS audio player. Full implementation would require AudioToolbox or AVFoundation.
/// </summary>
public class MacOSAudioPlayer : IAudioPlayer
{
    public Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        if (audioData == null || audioData.Length == 0)
            return Task.CompletedTask;

        // Calculate duration in milliseconds: (samples / sample rate) * 1000
        var durationMs = (int)((long)audioData.Length * 1000 / sampleRate);
        System.Diagnostics.Debug.WriteLine($"MacOS: Simulating {durationMs}ms audio playback on thread {Thread.CurrentThread.ManagedThreadId}");
        
        return Task.Delay(durationMs, cancellationToken).ContinueWith(_ =>
        {
            System.Diagnostics.Debug.WriteLine($"MacOS: Audio playback simulation completed");
        }, cancellationToken);
    }
}
