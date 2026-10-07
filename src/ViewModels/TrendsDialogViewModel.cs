using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Presentation;

namespace PentaGrammata.ViewModels;

public sealed class TrendsDialogViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IPracticeStatisticsExporter _statisticsExporter;
    private readonly ITrendsCsvExportService _csvExportService;
    private readonly IUiDispatcher _uiDispatcher;
    private IReadOnlyList<PracticeResultStatisticsRecord> _records = [];
    private string _summaryText = "Loading trend data...";
    private bool _showCharacterSeries = true;
    private bool _showAverageSeries = true;
    private bool _showErrorSeries = true;
    private bool _showLimitSeries = true;
    private bool _showNoiseSeries = true;
    private bool _showDailyRangeSeries = true;
    private bool _showQsbSeries = true;
    private string _exportStatusText = string.Empty;

    public event Action? CloseRequested;

    public IRelayCommand CloseCommand { get; }

    public IAsyncRelayCommand ExportCsvCommand { get; }

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

    public string ExportStatusText
    {
        get => _exportStatusText;
        private set => SetProperty(ref _exportStatusText, value);
    }

    public TrendsDialogViewModel(
        IPracticeResultStatisticsService statisticsService,
        IPracticeStatisticsExporter statisticsExporter,
        ITrendsCsvExportService csvExportService,
        IUiDispatcher uiDispatcher)
    {
        _statisticsService = statisticsService;
        _statisticsExporter = statisticsExporter;
        _csvExportService = csvExportService;
        _uiDispatcher = uiDispatcher;
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, CanExportCsv);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(), () => !ExportCsvCommand.IsRunning);
        ExportCsvCommand.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
            {
                CloseCommand.NotifyCanExecuteChanged();
            }
        };
    }

    private bool CanExportCsv() => _records.Count > 0;

    private async Task ExportCsvAsync()
    {
        ExportStatusText = string.Empty;
        try
        {
            var records = _records;
            var csv = await Task.Run(() =>
            {
                using var writer = new StringWriter();
                _statisticsExporter.Write(records, writer);
                return writer.ToString();
            });
            bool exported = await _csvExportService.ExportAsync(csv, CancellationToken.None).ConfigureAwait(true);
            ExportStatusText = exported ? "CSV exported." : "CSV export cancelled.";
        }
        catch (Exception ex)
        {
            ExportStatusText = $"CSV export failed: {ex.Message}";
        }
    }

    public async Task InitializeAsync()
    {
        var records = await _statisticsService.GetStatisticsRecordsAsync().ConfigureAwait(true);
        var points = await Task.Run(() => PreparePoints(records));
        await _uiDispatcher.InvokeAsync(() =>
        {
        _records = records;
        Points.Clear();
        foreach (var point in points) Points.Add(point);
        ExportCsvCommand.NotifyCanExecuteChanged();
        SummaryText = Points.Count == 0
            ? "No saved results yet."
            : $"{Points.Count} saved session(s), from {Points[0].RecordedAt:yyyy-MM-dd} to {Points[^1].RecordedAt:yyyy-MM-dd}. Mouse wheel: pan, Ctrl+wheel: zoom, drag: pan.";
        });
    }

    private static IReadOnlyList<PracticeTrendPoint> PreparePoints(IReadOnlyList<PracticeResultStatisticsRecord> records)
    {

        // Daily speed range: for each local calendar day, the highest and the lowest
        // AverageWpm among sessions whose error rate stayed below their error threshold. Both
        // ends come from the same passing set, so the chart can shade the day's range instead
        // of filling down to zero. Days with no passing session map to NaN on both ends so the
        // shading breaks across them.
        var dailyRangeByDay = records
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

        return records
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
            .OrderBy(x => x.RecordedAt)
            .ToArray();
    }
}
