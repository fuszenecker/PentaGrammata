using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.Services;

/// <summary>
/// RFC 4180 CSV exporter for saved practice sessions. Writes one row per record with every
/// persisted column, using invariant culture so numeric columns are stable across locales.
/// Pure and deterministic so it can be unit-tested without a window or store. The per-session
/// confusion rows are a separate one-to-many relation and are not included in this export.
/// </summary>
public sealed class PracticeStatisticsCsvExporter : IPracticeStatisticsExporter
{
    private const string Header =
        "RecordedAt,CharacterWpm,AverageWpm,CharacterCount,ErrorCount," +
        "ErrorRatePercent,ErrorThresholdPercent,NoiseType,NoiseLevelDb," +
        "NoiseBandwidthHz,AgcEnabled,AgcDelaySeconds,AgcMaxGainDb,ApfEnabled,ApfBandwidthHz,ApfPeakGainDb";

    public void Write(IEnumerable<PracticeResultStatisticsRecord> records, TextWriter writer)
    {
        writer.Write(Header);
        writer.Write("\r\n");

        foreach (var r in records)
        {
            writer.Write(r.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.CharacterWpm.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.AverageWpm.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.CharacterCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.ErrorCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.ErrorRatePercent.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.ErrorThresholdPercent.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(EscapeCsvField(r.NoiseType.ToString()));
            writer.Write(',');
            writer.Write(r.NoiseLevelDb.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.NoiseBandwidthHz.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.AgcEnabled ? "1" : "0");
            writer.Write(',');
            writer.Write(r.AgcDelaySeconds.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.AgcMaxGainDb.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.ApfEnabled ? "1" : "0");
            writer.Write(',');
            writer.Write(r.ApfBandwidthHz.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(r.ApfPeakGainDb.ToString(CultureInfo.InvariantCulture));
            writer.Write("\r\n");
        }
    }

    /// <summary>
    /// Escapes a CSV field according to RFC 4180: fields containing a comma, quote, or line
    /// break are wrapped in quotes with embedded quotes doubled.
    /// </summary>
    internal static string EscapeCsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
