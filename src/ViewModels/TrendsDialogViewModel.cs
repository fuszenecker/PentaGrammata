using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class TrendsDialogViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IPracticeStatisticsExporter _statisticsExporter;
    private IReadOnlyList<PracticeResultStatisticsRecord> _records = [];
    private string _summaryText = "Loading trend data...";
    private bool _showCharacterSeries = true;
    private bool _showAverageSeries = true;
    private bool _showErrorSeries = true;
    private bool _showLimitSeries = true;
    private bool _showNoiseSeries = true;
    private bool _showDailyRangeSeries = true;
    private bool _showQsbSeries = true;

    public event Action? CloseRequested;

    /// <summary>
    /// Raised when the user requests a CSV export, carrying the fully formatted
    /// CSV text. The view handles the actual file-save dialog, keeping the VM free
    /// of any window or storage dependency.
    /// </summary>
    public event Action<string>? ExportCsvRequested;

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand ExportCsvCommand { get; }

    public ObservableCollection<PracticeTrendPoint> Points { get; } = [];

    public bool ShowCharacterSeries
    {
        get => _showCharacterSeries;
        set => SetProperty(ref _showCharacterSeries, value);
    }

    public bool ShowAverageSeries
    {
        get => _showAverageSeries;
        set => SetProperty(ref _showAverageSeries, value);
    }

    public bool ShowErrorSeries
    {
        get => _showErrorSeries;
        set => SetProperty(ref _showErrorSeries, value);
    }

    public bool ShowLimitSeries
    {
        get => _showLimitSeries;
        set => SetProperty(ref _showLimitSeries, value);
    }

    public bool ShowNoiseSeries
    {
        get => _showNoiseSeries;
        set => SetProperty(ref _showNoiseSeries, value);
    }

    public bool ShowDailyRangeSeries
    {
        get => _showDailyRangeSeries;
        set => SetProperty(ref _showDailyRangeSeries, value);
    }

    public bool ShowQsbSeries
    {
        get => _showQsbSeries;
        set => SetProperty(ref _showQsbSeries, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public TrendsDialogViewModel(
        IPracticeResultStatisticsService statisticsService,
        IPracticeStatisticsExporter statisticsExporter)
    {
        _statisticsService = statisticsService;
        _statisticsExporter = statisticsExporter;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        ExportCsvCommand = new RelayCommand(ExportCsv, CanExportCsv);
    }

    private bool CanExportCsv() => _records.Count > 0;

    private void ExportCsv()
    {
        using var writer = new StringWriter();
        _statisticsExporter.Write(_records, writer);
        ExportCsvRequested?.Invoke(writer.ToString());
    }

    public async Task InitializeAsync()
    {
        Points.Clear();
        _records = await _statisticsService.GetStatisticsRecordsAsync().ConfigureAwait(false);

        // Daily speed range: for each local calendar day, the highest and the lowest
        // AverageWpm among sessions whose error rate stayed below their error threshold. Both
        // ends come from the same passing set, so the chart can shade the day's range instead
        // of filling down to zero. Days with no passing session map to NaN on both ends so the
        // shading breaks across them.
        var dailyRangeByDay = _records
            .GroupBy(r => r.RecordedAt.ToLocalTime().Date)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var passing = g.Where(r => r.ErrorRatePercent < r.ErrorThresholdPercent).ToArray();
                    return passing.Length == 0
                        ? (Min: double.NaN, Max: double.NaN)
                        : (Min: passing.Min(r => (double)r.AverageWpm), Max: passing.Max(r => (double)r.AverageWpm));
                });

        foreach (var point in _records
            .Select(r => new PracticeTrendPoint
            {
                RecordedAt = r.RecordedAt,
                CharacterWpm = r.CharacterWpm,
                AverageWpm = r.AverageWpm,
                ErrorRatePercent = r.ErrorRatePercent,
                ErrorThresholdPercent = r.ErrorThresholdPercent,
                NoiseLevelDb = r.NoiseLevelDb,
                QsbEnabled = r.QsbEnabled,
                // NaN for sessions recorded without fading, so the QSB line breaks there
                // instead of drawing a depth that was never applied.
                QsbDepthDb = r.QsbEnabled ? r.QsbDepthDb : double.NaN,
                QsbPeriodSeconds = r.QsbEnabled ? r.QsbPeriodSeconds : double.NaN,
                DailyMaxWpm = dailyRangeByDay.TryGetValue(r.RecordedAt.ToLocalTime().Date, out var range) ? range.Max : double.NaN,
                DailyMinWpm = dailyRangeByDay.TryGetValue(r.RecordedAt.ToLocalTime().Date, out var sameRange) ? sameRange.Min : double.NaN,
            })
            .OrderBy(x => x.RecordedAt))
        {
            Points.Add(point);
        }

        ExportCsvCommand.NotifyCanExecuteChanged();

        if (Points.Count == 0)
        {
            SummaryText = "No saved results yet.";
            return;
        }

        SummaryText = $"{Points.Count} saved session(s), from {Points[0].RecordedAt:yyyy-MM-dd} to {Points[^1].RecordedAt:yyyy-MM-dd}. Mouse wheel: pan, Ctrl+wheel: zoom, drag: pan.";
    }
}
