using System;

namespace PentaGrammata.Models;

public sealed class ConfusionObservation
{
    public DateTimeOffset RecordedAt { get; init; }
    public string ExpectedSymbol { get; init; } = string.Empty;
    public string ActualSymbol { get; init; } = string.Empty;
    public int Distance { get; init; }
    public int Count { get; init; }
}
