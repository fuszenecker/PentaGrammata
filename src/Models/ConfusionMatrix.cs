using System.Collections.Generic;

namespace PentaGrammata.Models;

/// <summary>
/// Outcome of building a confusion matrix from observations. When <see cref="Status"/> is not
/// <see cref="ConfusionMatrixStatus.Available"/>, <see cref="Matrix"/> is null and the caller
/// should surface the matching empty-state message; otherwise the matrix is ready to render.
/// </summary>
public sealed class ConfusionMatrixResult
{
    public ConfusionMatrixStatus Status { get; init; }
    public ConfusionMatrix? Matrix { get; init; }
}

public enum ConfusionMatrixStatus
{
    /// <summary>No substitution observations exist at all.</summary>
    NoSubstitutionData,

    /// <summary>Observations exist but every weighted score is zero (e.g. all too old).</summary>
    NoVisibleAfterWeighting,

    /// <summary>Weighted scores exist but none landed in the selected top symbols.</summary>
    NoVisibleAfterFiltering,

    /// <summary>A renderable matrix is available.</summary>
    Available,
}

/// <summary>
/// A square heat matrix over the top confusion symbols. <see cref="Cells"/> is indexed
/// [expectedSymbol, actualSymbol]; <see cref="Symbols"/> gives the row/column order.
/// </summary>
public sealed class ConfusionMatrix
{
    public IReadOnlyList<string> Symbols { get; init; } = [];
    public double[,] Cells { get; init; } = new double[0, 0];
    public double TotalScore { get; init; }
    public double MaxScore { get; init; }
}

/// <summary>A symbol paired with its aggregate weighted confusion score.</summary>
public sealed class WeightedSymbolCount
{
    public string Symbol { get; init; } = string.Empty;
    public double Score { get; init; }
}
