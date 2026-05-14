using System.Collections.Generic;

namespace PentaGrammata.Models;

public sealed class PracticeResult
{
    public List<PracticeResultRow> Rows { get; init; } = [];
    public int CharacterCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRatePercent { get; init; }
}
