using System;
using System.Collections.Generic;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Pure analysis over saved sessions: pairs each session's average speed, signal-to-noise ratio
/// or QSB depth with its error rate
/// inside a rolling day window and fits a least-squares line through the pairs. Has no
/// presentation or persistence dependencies, so the correlation view model stays limited to
/// rendering and the math is independently testable.
/// </summary>
public interface ICorrelationAnalysisService
{
    /// <summary>
    /// Builds the scatter of <paramref name="axis"/> against error rate and its linear fit from
    /// the sessions recorded in the <paramref name="windowDays"/> days before <paramref name="now"/>
    /// that carry that quantity (only sessions with noise have a signal-to-noise ratio, only
    /// sessions with QSB a fade depth).
    /// </summary>
    ErrorCorrelation Analyze(IReadOnlyList<PracticeResultStatisticsRecord> records, CorrelationAxis axis, double windowDays, DateTimeOffset now);
}
