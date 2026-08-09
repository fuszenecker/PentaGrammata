using System;
using System.Runtime.InteropServices;

using PentaGrammata.Interfaces;

namespace PentaGrammata.Players;

public static class AudioPlayerFactory
{
    public static IAudioPlayer Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsAudioPlayer();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxAudioPlayer();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSAudioPlayer();

        // Fail loudly rather than silently pretending to play audio on a platform
        // we have no real implementation for.
        throw new PlatformNotSupportedException(
            $"No audio player is available for this platform: {RuntimeInformation.OSDescription}.");
    }
}
