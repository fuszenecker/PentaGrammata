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
    private bool _showDailyMaxSeries = true;

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

    public bool ShowDailyMaxSeries
    {
        get => _showDailyMaxSeries;
        set => SetProperty(ref _showDailyMaxSeries, value);
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

        // Daily-max speed: for each local calendar day, take the highest AverageWpm among
        // sessions whose error rate stayed below their error threshold. Days with no such
        // session map to NaN so the chart can break the dashed line across them.
        var dailyMaxByDay = _records
            .GroupBy(r => r.RecordedAt.ToLocalTime().Date)
            .ToDictionary(
                g => g.Key,
                g => g.Any(r => r.ErrorRatePercent < r.ErrorThresholdPercent)
                    ? g.Where(r => r.ErrorRatePercent < r.ErrorThresholdPercent).Max(r => (double)r.AverageWpm)
                    : double.NaN);

        foreach (var point in _records
            .Select(r => new PracticeTrendPoint
            {
                RecordedAt = r.RecordedAt,
                CharacterWpm = r.CharacterWpm,
                AverageWpm = r.AverageWpm,
                ErrorRatePercent = r.ErrorRatePercent,
                ErrorThresholdPercent = r.ErrorThresholdPercent,
                NoiseLevelDb = r.NoiseLevelDb,
                DailyMaxWpm = dailyMaxByDay.TryGetValue(r.RecordedAt.ToLocalTime().Date, out var max) ? max : double.NaN,
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
