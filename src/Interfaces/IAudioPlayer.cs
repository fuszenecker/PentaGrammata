using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Interfaces;

public interface IAudioPlayer
{
    Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken);
}