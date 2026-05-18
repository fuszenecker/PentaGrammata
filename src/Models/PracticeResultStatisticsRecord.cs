using System;

namespace PentaGrammata.Models;

public sealed class PracticeResultStatisticsRecord
{
    public DateTimeOffset RecordedAt { get; init; }
    public int CharacterWpm { get; init; }
    public int AverageWpm { get; init; }
    public int CharacterCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRatePercent { get; init; }
}
