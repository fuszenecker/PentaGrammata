using System;
using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

public interface IMorsePlayer
{
    Task PlayMorseCodeAsync(string morseCode, int charWpm, int averageWpm, int sampleRate, int beepRampMs, CancellationToken cancellationToken);
}