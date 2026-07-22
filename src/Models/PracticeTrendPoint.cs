using System;

namespace PentaGrammata.Models;

public sealed class PracticeTrendPoint
{
    public DateTimeOffset RecordedAt { get; init; }
    public int CharacterWpm { get; init; }
    public int AverageWpm { get; init; }
    public double ErrorRatePercent { get; init; }
    public double ErrorThresholdPercent { get; init; }
    public double NoiseLevelDb { get; init; }
}
