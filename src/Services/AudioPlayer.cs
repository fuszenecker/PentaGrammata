using System.Runtime.InteropServices;

namespace PentaGrammata.Services;

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
        
        // Fallback for any other platform
        return new MacOSAudioPlayer();
    }
}
