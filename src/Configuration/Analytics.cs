using Promptuarium.Serializer;

namespace PentaGrammata.Configuration;

[PromptuariumComment("The statistics dialogs")]
public sealed class Analytics
{
    [PromptuariumComment("The half-life of the character confusion statistics, in days: older mistakes weigh less")]
    public double ConfusionsHalfLifeDays { get; set; } = 1.0;

    /// <summary>
    /// Length of the correlation dialog's analysis window, in days: sessions older than this
    /// are left out of the speed-versus-error scatter and its fitted line.
    /// </summary>
    [PromptuariumComment("The analysis window of the speed versus error rate correlation dialog, in days")]
    public double CorrelationWindowDays { get; set; } = 10.0;

    public Analytics Clone() => new()
    {
        ConfusionsHalfLifeDays = ConfusionsHalfLifeDays,
        CorrelationWindowDays = CorrelationWindowDays,
    };
}
