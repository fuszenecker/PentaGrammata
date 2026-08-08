using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class PracticeStatisticsCsvExporterTests
{
    private static string Export(params PracticeResultStatisticsRecord[] records)
    {
        using var writer = new StringWriter();
        new PracticeStatisticsCsvExporter().Write(records, writer);
        return writer.ToString();
    }

    [TestMethod]
    public void EscapeCsvField_QuotesSpecialCharacters()
    {
        Assert.AreEqual("plain", PracticeStatisticsCsvExporter.EscapeCsvField("plain"));
        Assert.AreEqual("\"a,b\"", PracticeStatisticsCsvExporter.EscapeCsvField("a,b"));
        Assert.AreEqual("\"a\"\"b\"", PracticeStatisticsCsvExporter.EscapeCsvField("a\"b"));
        Assert.AreEqual("\"a\nb\"", PracticeStatisticsCsvExporter.EscapeCsvField("a\nb"));
    }

    [TestMethod]
    public void Write_Empty_ProducesOnlyHeader()
    {
        var csv = Export(Array.Empty<PracticeResultStatisticsRecord>());

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, lines.Length);
        Assert.AreEqual(
            "RecordedAt,CharacterWpm,AverageWpm,CharacterCount,ErrorCount,ErrorRatePercent,ErrorThresholdPercent,NoiseType,NoiseLevelDb,NoiseBandwidthHz,AgcEnabled,AgcDelaySeconds,ApfEnabled,ApfBandwidthHz,ApfPeakGainDb",
            lines[0]);
    }

    [TestMethod]
    public void Write_RendersAllColumnsWithInvariantCulture()
    {
        var record = new PracticeResultStatisticsRecord
        {
            RecordedAt = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero),
            CharacterWpm = 20,
            AverageWpm = 15,
            CharacterCount = 120,
            ErrorCount = 7,
            ErrorRatePercent = 5.5,
            ErrorThresholdPercent = 5.0,
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -10.25,
            NoiseBandwidthHz = 600,
            AgcEnabled = true,
            AgcDelaySeconds = 0.5,
            ApfEnabled = false,
            ApfBandwidthHz = 100,
            ApfPeakGainDb = 3.0,
        };

        var csv = Export(record);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(
            "2026-08-07T12:00:00.0000000+00:00,20,15,120,7,5.5,5,Gaussian,-10.25,600,1,0.5,0,100,3",
            lines[1]);
    }

    [TestMethod]
    public void Write_RendersNoiseTypeAsName()
    {
        var record = new PracticeResultStatisticsRecord
        {
            RecordedAt = DateTimeOffset.UnixEpoch,
            NoiseType = NoiseType.None,
        };

        var csv = Export(record);

        StringAssert.Contains(csv, ",None,");
    }

    [TestMethod]
    public void Write_PreservesRecordOrder()
    {
        var records = new List<PracticeResultStatisticsRecord>
        {
            new()
            {
                RecordedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CharacterWpm = 10,
            },
            new()
            {
                RecordedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                CharacterWpm = 12,
            },
        };

        var csv = Export(records.ToArray());

        var dataLines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(3, dataLines.Length); // header + 2 rows
        StringAssert.Contains(dataLines[1], "2026-01-01");
        StringAssert.Contains(dataLines[2], "2026-01-02");
    }

    [TestMethod]
    public void Write_StreamsDirectlyToWriterWithoutBufferingWholeExport()
    {
        // Streaming contract: the header is written before any record is enumerated, so a
        // caller writing straight to a file stream sees output even with zero records.
        var written = new List<string>();
        var probingWriter = new ProbingTextWriter(written);

        new PracticeStatisticsCsvExporter().Write(Array.Empty<PracticeResultStatisticsRecord>(), probingWriter);

        Assert.IsGreaterThan(0, written.Count);
        StringAssert.Contains(written[0], "RecordedAt");
    }

    private sealed class ProbingTextWriter : StringWriter
    {
        private readonly List<string> _written;
        public ProbingTextWriter(List<string> written) => _written = written;
        public override void Write(string? value)
        {
            _written.Add(value ?? string.Empty);
            base.Write(value);
        }
    }
}
