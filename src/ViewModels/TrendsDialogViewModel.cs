using System;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using PentaGrammata.Interfaces;

namespace PentaGrammata.ViewModels;

public sealed class TrendsDialogViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private PlotModel _plotModel;
    private string _summaryText = "Loading trend data...";

    public event Action? CloseRequested;

    public IRelayCommand CloseCommand { get; }

    public PlotModel PlotModel
    {
        get => _plotModel;
        private set => SetProperty(ref _plotModel, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public TrendsDialogViewModel(IPracticeResultStatisticsStore statisticsStore)
    {
        _statisticsStore = statisticsStore;
        _plotModel = CreateEmptyModel("Loading trend data...");
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public async Task InitializeAsync()
    {
        var points = await _statisticsStore.GetTrendPointsAsync().ConfigureAwait(false);
        if (points.Count == 0)
        {
            PlotModel = CreateEmptyModel("No saved results yet.");
            SummaryText = "No saved results yet.";
            return;
        }

        var orderedPoints = points.OrderBy(x => x.RecordedAt).ToArray();

        var plot = new PlotModel
        {
            Title = "Practice Trends",
            Subtitle = "Mouse wheel = zoom, drag = pan"
        };

        plot.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Time",
            StringFormat = "yyyy-MM-dd\nHH:mm",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            IsPanEnabled = true,
            IsZoomEnabled = true
        });

        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Key = "SpeedAxis",
            Title = "WPM",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Right,
            Key = "PercentAxis",
            Title = "Error (%)",
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Right,
            PositionTier = 1,
            Key = "NoiseAxis",
            Title = "Noise (dB)",
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            IsPanEnabled = false,
            IsZoomEnabled = false
        });

        var characterSeries = new LineSeries
        {
            Title = "Character speed",
            Color = OxyColor.Parse("#1D4ED8"),
            YAxisKey = "SpeedAxis"
        };

        var averageSeries = new LineSeries
        {
            Title = "Average speed",
            Color = OxyColor.Parse("#0891B2"),
            YAxisKey = "SpeedAxis"
        };

        var errorSeries = new LineSeries
        {
            Title = "Error rate",
            Color = OxyColor.Parse("#DC2626"),
            YAxisKey = "PercentAxis"
        };

        var thresholdSeries = new LineSeries
        {
            Title = "Error limit",
            Color = OxyColor.Parse("#F59E0B"),
            LineStyle = LineStyle.Dash,
            YAxisKey = "PercentAxis"
        };

        var noiseSeries = new LineSeries
        {
            Title = "Noise",
            Color = OxyColor.Parse("#16A34A"),
            YAxisKey = "NoiseAxis"
        };

        foreach (var point in orderedPoints)
        {
            var x = DateTimeAxis.ToDouble(point.RecordedAt.UtcDateTime);
            characterSeries.Points.Add(new DataPoint(x, point.CharacterWpm));
            averageSeries.Points.Add(new DataPoint(x, point.AverageWpm));
            errorSeries.Points.Add(new DataPoint(x, point.ErrorRatePercent));
            thresholdSeries.Points.Add(new DataPoint(x, point.ErrorThresholdPercent));
            noiseSeries.Points.Add(new DataPoint(x, point.NoiseLevelDb));
        }

        plot.Series.Add(characterSeries);
        plot.Series.Add(averageSeries);
        plot.Series.Add(errorSeries);
        plot.Series.Add(thresholdSeries);
        plot.Series.Add(noiseSeries);

        PlotModel = plot;
        SummaryText = $"{orderedPoints.Length} saved session(s), from {orderedPoints[0].RecordedAt:yyyy-MM-dd} to {orderedPoints[^1].RecordedAt:yyyy-MM-dd}.";
    }

    private static PlotModel CreateEmptyModel(string title)
    {
        return new PlotModel
        {
            Title = title
        };
    }
}
