using System.Runtime.InteropServices;

namespace PentaGrammata.Services;

public static class AudioPlayerFactory
{
    public static IAudioPlayer Create() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsAudioPlayer()
            : new LinuxAudioPlayer();
}
