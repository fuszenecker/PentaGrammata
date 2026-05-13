using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

public interface IAudioPlayer
{
    Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken);
}