using System;

namespace PentaGrammata.Interfaces;

/// <summary>Creates signal renderers that share the application's configured noise sources.</summary>
public interface IMorseSignalRendererFactory
{
    IMorseSignalRenderer Create(Random random);
}
