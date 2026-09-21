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

    /// <summary>Whether QSB (signal fading) was active for the session.</summary>
    public bool QsbEnabled { get; init; }

    /// <summary>
    /// Deepest fade below full signal strength, in decibels, or <see cref="double.NaN"/>
    /// when the session ran without QSB. The chart draws this in the noise band, so the
    /// solid red line breaks across sessions recorded with fading off.
    /// </summary>
    public double QsbDepthDb { get; init; }

    /// <summary>
    /// Fade period, in seconds, or <see cref="double.NaN"/> when the session ran without QSB.
    /// </summary>
    public double QsbPeriodSeconds { get; init; }

    /// <summary>
    /// The maximum <see cref="AverageWpm"/> reached on <see cref="RecordedAt"/>'s local
    /// day across sessions whose error rate was below their error threshold. This is a
    /// per-day aggregate repeated on every session of the day so the band's upper edge can
    /// align with the session-indexed x-axis. <see cref="double.NaN"/> when no session on
    /// that day cleared the error threshold, and the shading breaks (gaps) there.
    /// </summary>
    public double DailyMaxWpm { get; init; }

    /// <summary>
    /// The minimum <see cref="AverageWpm"/> reached on <see cref="RecordedAt"/>'s local day
    /// across sessions whose error rate was below their error threshold, computed exactly like
    /// <see cref="DailyMaxWpm"/> but taking the minimum. It forms the lower edge of the shaded
    /// daily band, so the band spans the day's passing range instead of starting at zero.
    /// <see cref="double.NaN"/> on the same days <see cref="DailyMaxWpm"/> is NaN.
    /// </summary>
    public double DailyMinWpm { get; init; }
}
