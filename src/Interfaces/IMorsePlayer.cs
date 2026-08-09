using System;
using System.Threading;
using System.Threading.Tasks;
using PentaGrammata.Players;

namespace PentaGrammata.Interfaces;

public interface IMorsePlayer
{
    Task PlayMorseCodeAsync(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken);
}