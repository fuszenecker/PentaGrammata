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

    /// <summary>
    /// The maximum <see cref="AverageWpm"/> reached on <see cref="RecordedAt"/>'s local
    /// day across sessions whose error rate was below their error threshold. This is a
    /// per-day aggregate repeated on every session of the day so the daily-max line can
    /// align with the session-indexed x-axis. <see cref="double.NaN"/> when no session on
    /// that day cleared the error threshold — the dashed line breaks (gaps) there.
    /// </summary>
    public double DailyMaxWpm { get; init; }
}
