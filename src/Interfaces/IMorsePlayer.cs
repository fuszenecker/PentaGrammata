using System;
using System.Threading;
using System.Threading.Tasks;

namespace PentaGrammata.Services;

public interface IMorsePlayer
{
    Task PlayMorseCodeAsync(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken);
}