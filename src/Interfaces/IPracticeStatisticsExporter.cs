using System.Collections.Generic;
using System.IO;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Serializes practice statistics records to a portable text format. The default
/// implementation writes RFC 4180 CSV with invariant-culture numeric columns, streaming
/// directly to a <see cref="TextWriter"/> so callers can export to a string (via
/// <see cref="StringWriter"/>) or straight to a file without buffering the whole export.
/// </summary>
public interface IPracticeStatisticsExporter
{
    void Write(IEnumerable<PracticeResultStatisticsRecord> records, TextWriter writer);
}
