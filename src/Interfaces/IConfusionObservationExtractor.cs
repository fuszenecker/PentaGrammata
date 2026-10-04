using System;
using System.Collections.Generic;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Derives per-symbol confusion observations from the sent/received rows of one
/// practice session, for persistence alongside its statistics record.
/// </summary>
public interface IConfusionObservationExtractor
{
    IReadOnlyList<ConfusionObservation> Extract(IReadOnlyList<PracticeResultRow> rows, DateTimeOffset recordedAt);
}
