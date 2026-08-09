using System;
using System.Collections.Generic;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Pure analysis over recorded confusion observations: half-life-weighted decay scoring,
/// top-symbol selection, heat-matrix construction, and "practice confusions" character-set
/// generation. Has no presentation or persistence dependencies, so the confusion matrix view
/// model stays limited to rendering (brushes, display text) and the analysis is independently
/// testable.
/// </summary>
public interface IConfusionAnalysisService
{
    /// <summary>
    /// Builds the confusion matrix for the given observations using half-life decay measured
    /// against <paramref name="now"/>.
    /// </summary>
    ConfusionMatrixResult BuildMatrix(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now);

    /// <summary>
    /// Per-symbol aggregate weighted scores (expected and actual symbols both counted), used
    /// to decide whether a practice-confusions set can be generated.
    /// </summary>
    IReadOnlyList<WeightedSymbolCount> WeightedSymbolCounts(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now);

    /// <summary>
    /// Builds a character set weighted toward the most confused symbols, scaled to
    /// <paramref name="targetSymbolCount"/> entries, or null if there is no data.
    /// </summary>
    string? BuildPracticeConfusionsCharacterSet(IReadOnlyList<ConfusionObservation> observations, double halfLifeDays, DateTimeOffset now, int targetSymbolCount);
}
