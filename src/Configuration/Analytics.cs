namespace PentaGrammata.Configuration;

public sealed class Analytics
{
    public double ConfusionsHalfLifeDays { get; set; } = 1.0;

    public Analytics Clone() => new()
    {
        ConfusionsHalfLifeDays = ConfusionsHalfLifeDays,
    };
}
