using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class TrendsDialogViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private string _summaryText = "Loading trend data...";
    private bool _showCharacterSeries = true;
    private bool _showAverageSeries = true;
    private bool _showErrorSeries = true;
    private bool _showLimitSeries = true;
    private bool _showNoiseSeries = true;

    public event Action? CloseRequested;

    public IRelayCommand CloseCommand { get; }

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

    public TrendsDialogViewModel(IPracticeResultStatisticsStore statisticsStore)
    {
        _statisticsStore = statisticsStore;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public async Task InitializeAsync()
    {
        Points.Clear();
        var points = await _statisticsStore.GetTrendPointsAsync().ConfigureAwait(false);
        foreach (var point in points.OrderBy(x => x.RecordedAt))
        {
            Points.Add(point);
        }

        if (Points.Count == 0)
        {
            SummaryText = "No saved results yet.";
            return;
        }

        SummaryText = $"{Points.Count} saved session(s), from {Points[0].RecordedAt:yyyy-MM-dd} to {Points[^1].RecordedAt:yyyy-MM-dd}. Mouse wheel: pan, Ctrl+wheel: zoom, drag: pan.";
    }
}
