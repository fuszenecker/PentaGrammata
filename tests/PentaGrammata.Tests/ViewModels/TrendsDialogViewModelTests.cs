using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class TrendsDialogViewModelTests
{
    [TestMethod]
    public void BuildCsv_Empty_ProducesOnlyHeader()
    {
        var csv = TrendsDialogViewModel.BuildCsv(Array.Empty<PracticeResultStatisticsRecord>());

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, lines.Length);
        Assert.AreEqual(
            "RecordedAt,CharacterWpm,AverageWpm,CharacterCount,ErrorCount,ErrorRatePercent,ErrorThresholdPercent,NoiseType,NoiseLevelDb,NoiseBandwidthHz,AgcEnabled,AgcDelaySeconds,ApfEnabled,ApfBandwidthHz,ApfPeakGainDb",
            lines[0]);
    }

    [TestMethod]
    public void BuildCsv_RendersAllColumnsWithInvariantCulture()
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

        var csv = TrendsDialogViewModel.BuildCsv(new[] { record });

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(
            "2026-08-07T12:00:00.0000000+00:00,20,15,120,7,5.5,5,Gaussian,-10.25,600,1,0.5,0,100,3",
            lines[1]);
    }

    [TestMethod]
    public void BuildCsv_RendersNoiseTypeAsName()
    {
        var record = new PracticeResultStatisticsRecord
        {
            RecordedAt = DateTimeOffset.UnixEpoch,
            NoiseType = NoiseType.None,
        };

        var csv = TrendsDialogViewModel.BuildCsv(new[] { record });

        StringAssert.Contains(csv, ",None,");
    }

    [TestMethod]
    public void BuildCsv_PreservesRecordOrder()
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

        var csv = TrendsDialogViewModel.BuildCsv(records);

        var dataLines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(3, dataLines.Length); // header + 2 rows
        StringAssert.Contains(dataLines[1], "2026-01-01");
        StringAssert.Contains(dataLines[2], "2026-01-02");
    }
}
