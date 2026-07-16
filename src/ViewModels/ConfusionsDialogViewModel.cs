using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class ConfusionsDialogViewModel : ViewModelBase
{
    private const double DecayWindowDays = 30d;

    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private PlotModel _plotModel;
    private string _summaryText = "Loading confusion matrix...";

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

    public ConfusionsDialogViewModel(IPracticeResultStatisticsStore statisticsStore)
    {
        _statisticsStore = statisticsStore;
        _plotModel = CreateEmptyModel("Loading confusion matrix...");
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public async Task InitializeAsync()
    {
        var observations = await _statisticsStore.GetConfusionObservationsAsync().ConfigureAwait(false);
        if (observations.Count == 0)
        {
            PlotModel = CreateEmptyModel("No confusion data yet. Save at least one result first.");
            SummaryText = "No confusion data yet.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var weighted = observations
            .Select(observation => new
            {
                observation.ExpectedSymbol,
                observation.ActualSymbol,
                Score = CalculateScore(observation, now)
            })
            .Where(x => x.Score > 0)
            .ToArray();

        if (weighted.Length == 0)
        {
            PlotModel = CreateEmptyModel("No confusion data after weighting.");
            SummaryText = "No visible confusion data after weighting.";
            return;
        }

        var symbolTotals = weighted
            .GroupBy(x => x.ExpectedSymbol)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Score), StringComparer.Ordinal);

        foreach (var grouped in weighted.GroupBy(x => x.ActualSymbol))
        {
            if (symbolTotals.TryGetValue(grouped.Key, out var existing))
            {
                symbolTotals[grouped.Key] = existing + grouped.Sum(x => x.Score);
            }
            else
            {
                symbolTotals[grouped.Key] = grouped.Sum(x => x.Score);
            }
        }

        var symbols = symbolTotals
            .OrderByDescending(x => x.Value)
            .Take(24)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var symbolIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < symbols.Length; i++)
        {
            symbolIndex[symbols[i]] = i;
        }

        var matrix = new double[symbols.Length, symbols.Length];
        var totalScore = 0d;

        foreach (var item in weighted)
        {
            if (!symbolIndex.TryGetValue(item.ExpectedSymbol, out var rowIndex)
                || !symbolIndex.TryGetValue(item.ActualSymbol, out var columnIndex))
            {
                continue;
            }

            matrix[rowIndex, columnIndex] += item.Score;
            totalScore += item.Score;
        }

        var maxCellScore = matrix.Cast<double>().DefaultIfEmpty(0).Max();
        if (maxCellScore <= 0)
        {
            PlotModel = CreateEmptyModel("No confusion data after filtering.");
            SummaryText = "No visible confusion data after filtering.";
            return;
        }

        var plot = new PlotModel
        {
            Title = "Confusion Matrix",
            Subtitle = "Vertical = expected, horizontal = received. '_' means insertion/deletion gap."
        };

        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -0.5,
            Maximum = symbols.Length - 0.5,
            MajorStep = 1,
            MinorStep = 1,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            Title = "Received",
            LabelFormatter = value => FormatSymbolLabel(value, symbols)
        });

        plot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = -0.5,
            Maximum = symbols.Length - 0.5,
            MajorStep = 1,
            MinorStep = 1,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            Title = "Expected",
            LabelFormatter = value => FormatSymbolLabel(value, symbols)
        });

        plot.Axes.Add(new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Palette = OxyPalette.Interpolate(256, OxyColors.Black, OxyColor.Parse("#F97316"), OxyColor.Parse("#DC2626")),
            Minimum = 0,
            Maximum = maxCellScore,
            Title = "Weighted confusion"
        });

        plot.Series.Add(new HeatMapSeries
        {
            X0 = -0.5,
            X1 = symbols.Length - 0.5,
            Y0 = -0.5,
            Y1 = symbols.Length - 0.5,
            Interpolate = false,
            RenderMethod = HeatMapRenderMethod.Rectangles,
            Data = matrix
        });

        PlotModel = plot;
        SummaryText = string.Format(
            CultureInfo.InvariantCulture,
            "{0} weighted observations across {1} symbols (decay window: {2} days).",
            totalScore,
            symbols.Length,
            DecayWindowDays);
    }

    private static string FormatSymbolLabel(double axisValue, IReadOnlyList<string> symbols)
    {
        var index = (int)Math.Round(axisValue, MidpointRounding.AwayFromZero);
        if (index < 0 || index >= symbols.Count)
        {
            return string.Empty;
        }

        return symbols[index];
    }

    private static double CalculateScore(ConfusionObservation observation, DateTimeOffset now)
    {
        if (observation.Count <= 0)
        {
            return 0;
        }

        var ageDays = Math.Max(0, (now - observation.RecordedAt).TotalDays);
        var decay = Math.Exp(-ageDays / DecayWindowDays);
        var distanceFactor = 1d / Math.Max(1, observation.Distance);
        return observation.Count * decay * distanceFactor;
    }

    private static PlotModel CreateEmptyModel(string title)
    {
        return new PlotModel
        {
            Title = title
        };
    }
}
