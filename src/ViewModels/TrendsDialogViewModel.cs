using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class TrendsDialogViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsService _statisticsService;
    private IReadOnlyList<PracticeResultStatisticsRecord> _records = [];
    private string _summaryText = "Loading trend data...";
    private bool _showCharacterSeries = true;
    private bool _showAverageSeries = true;
    private bool _showErrorSeries = true;
    private bool _showLimitSeries = true;
    private bool _showNoiseSeries = true;

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

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public TrendsDialogViewModel(IPracticeResultStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        ExportCsvCommand = new RelayCommand(ExportCsv, CanExportCsv);
    }

    private bool CanExportCsv() => _records.Count > 0;

    private void ExportCsv()
    {
        var csv = BuildCsv(_records);
        ExportCsvRequested?.Invoke(csv);
    }

    /// <summary>
    /// Escapes a CSV field according to RFC 4180. Current exported fields are safe scalar
    /// values, but keeping this helper here prevents future string columns from corrupting
    /// the output when they contain delimiters, quotes, or line breaks.
    /// </summary>
    internal static string EscapeCsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    /// <summary>
    /// Renders the supplied session records as RFC 4180-ish CSV — one row per
    /// saved session with every persisted column — using invariant culture so the
    /// numeric columns are stable across locales. Pure and deterministic so it can
    /// be unit-tested without a window or store. The per-session confusion rows
    /// are a separate one-to-many relation and are not included in this export.
    /// </summary>
    internal static string BuildCsv(IEnumerable<PracticeResultStatisticsRecord> records)
    {
        const string header =
            "RecordedAt,CharacterWpm,AverageWpm,CharacterCount,ErrorCount," +
            "ErrorRatePercent,ErrorThresholdPercent,NoiseType,NoiseLevelDb," +
            "NoiseBandwidthHz,AgcEnabled,AgcDelaySeconds,ApfEnabled,ApfBandwidthHz,ApfPeakGainDb";

        var sb = new StringBuilder();
        sb.Append(header).Append("\r\n");
        foreach (var r in records)
        {
            sb.Append(r.RecordedAt.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.CharacterWpm.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.AverageWpm.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.CharacterCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ErrorCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ErrorRatePercent.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ErrorThresholdPercent.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsvField(r.NoiseType.ToString())).Append(',');

            sb.Append(r.NoiseLevelDb.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.NoiseBandwidthHz.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.AgcEnabled ? "1" : "0").Append(',');
            sb.Append(r.AgcDelaySeconds.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ApfEnabled ? "1" : "0").Append(',');
            sb.Append(r.ApfBandwidthHz.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ApfPeakGainDb.ToString(CultureInfo.InvariantCulture));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    public async Task InitializeAsync()
    {
        Points.Clear();
        _records = await _statisticsService.GetStatisticsRecordsAsync().ConfigureAwait(false);

        foreach (var point in _records
            .Select(r => new PracticeTrendPoint
            {
                RecordedAt = r.RecordedAt,
                CharacterWpm = r.CharacterWpm,
                AverageWpm = r.AverageWpm,
                ErrorRatePercent = r.ErrorRatePercent,
                ErrorThresholdPercent = r.ErrorThresholdPercent,
                NoiseLevelDb = r.NoiseLevelDb,
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
