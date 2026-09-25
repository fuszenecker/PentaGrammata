namespace PentaGrammata.Configuration;

public sealed class Analytics
{
    public double ConfusionsHalfLifeDays { get; set; } = 1.0;

    /// <summary>
    /// Length of the correlation dialog's analysis window, in days: sessions older than this
    /// are left out of the speed-versus-error scatter and its fitted line.
    /// </summary>
    public double CorrelationWindowDays { get; set; } = 10.0;

    public Analytics Clone() => new()
    {
        ConfusionsHalfLifeDays = ConfusionsHalfLifeDays,
        CorrelationWindowDays = CorrelationWindowDays,
    };
}
