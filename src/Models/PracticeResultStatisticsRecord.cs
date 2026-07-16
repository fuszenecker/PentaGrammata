using System;
using System.Collections.Generic;
using PentaGrammata.Configuration;

namespace PentaGrammata.Models;

public sealed class PracticeResultStatisticsRecord
{
    public DateTimeOffset RecordedAt { get; init; }
    public int CharacterWpm { get; init; }
    public int AverageWpm { get; init; }
    public int CharacterCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRatePercent { get; init; }
    public double ErrorThresholdPercent { get; init; }

    public NoiseType NoiseType { get; init; }
    public double NoiseLevelDb { get; init; }
    public double NoiseBandwidthHz { get; init; }
    public bool AgcEnabled { get; init; }
    public double AgcDelaySeconds { get; init; }
    public bool ApfEnabled { get; init; }
    public double ApfBandwidthHz { get; init; }
    public double ApfPeakGainDb { get; init; }

    public IReadOnlyList<ConfusionObservation> Confusions { get; init; } = [];
}
