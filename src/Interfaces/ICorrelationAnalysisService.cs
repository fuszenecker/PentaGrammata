using System;
using System.Collections.Generic;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Pure analysis over saved sessions: pairs each session's average speed with its error rate
/// inside a rolling day window and fits a least-squares line through the pairs. Has no
/// presentation or persistence dependencies, so the correlation view model stays limited to
/// rendering and the math is independently testable.
/// </summary>
public interface ICorrelationAnalysisService
{
    /// <summary>
    /// Builds the speed-versus-error scatter and its linear fit from the sessions recorded in
    /// the <paramref name="windowDays"/> days before <paramref name="now"/>.
    /// </summary>
    SpeedErrorCorrelation Analyze(IReadOnlyList<PracticeResultStatisticsRecord> records, double windowDays, DateTimeOffset now);
}
