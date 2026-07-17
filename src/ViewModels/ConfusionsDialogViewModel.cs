using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class ConfusionsDialogViewModel : ViewModelBase
{
    // The half-life is how many days it takes for an observation's weight to halve.
    // Default and bounds for the user-facing control.
    private const double DefaultHalfLifeDays = 1d;
    private const double MinHalfLifeDays = 1d;
    private const double MaxHalfLifeDays = 365d;

    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private string _summaryText = "Loading confusion matrix...";
    private double _halfLifeDays = DefaultHalfLifeDays;

    // Cached so adjusting the half-life recomputes the matrix without re-querying.
    private IReadOnlyList<ConfusionObservation> _observations = [];

    public event Action? CloseRequested;

    public IRelayCommand CloseCommand { get; }

    public ObservableCollection<string> ColumnHeaders { get; } = [];

    public ObservableCollection<ConfusionMatrixRowViewModel> Rows { get; } = [];

    public double MinHalfLife => MinHalfLifeDays;

    public double MaxHalfLife => MaxHalfLifeDays;

    /// <summary>
    /// Retention half-life ("felezési idő") in days: older confusions weigh less, and an
    /// observation this many days old counts for half of a fresh one. Setting it recomputes
    /// the matrix from the already-loaded observations.
    /// </summary>
    public double HalfLifeDays
    {
        get => _halfLifeDays;
        set
        {
            var clamped = Math.Clamp(value, MinHalfLifeDays, MaxHalfLifeDays);
            if (SetProperty(ref _halfLifeDays, clamped))
            {
                Rebuild();
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public ConfusionsDialogViewModel(IPracticeResultStatisticsStore statisticsStore)
    {
        _statisticsStore = statisticsStore;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public async Task InitializeAsync()
    {
        _observations = await _statisticsStore.GetConfusionObservationsAsync().ConfigureAwait(false);
        Rebuild();
    }

    private void Rebuild()
    {
        ColumnHeaders.Clear();
        Rows.Clear();

        var observations = _observations;
        if (observations.Count == 0)
        {
            SummaryText = "No confusion data yet.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var weighted = observations
            .Select(observation => new
            {
                observation.ExpectedSymbol,
                observation.ActualSymbol,
                Score = CalculateScore(observation, now, _halfLifeDays)
            })
            .Where(x => x.Score > 0)
            .ToArray();

        if (weighted.Length == 0)
        {
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

        foreach (var symbol in symbols)
        {
            ColumnHeaders.Add(symbol);
        }

        var symbolIndex = symbols
            .Select((symbol, index) => new { symbol, index })
            .ToDictionary(x => x.symbol, x => x.index, StringComparer.Ordinal);

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

        var maxScore = matrix.Cast<double>().DefaultIfEmpty(0).Max();
        if (maxScore <= 0)
        {
            SummaryText = "No visible confusion data after filtering.";
            return;
        }

        for (var row = 0; row < symbols.Length; row++)
        {
            var rowVm = new ConfusionMatrixRowViewModel
            {
                ExpectedSymbol = symbols[row]
            };

            for (var column = 0; column < symbols.Length; column++)
            {
                var score = matrix[row, column];
                var normalized = score / maxScore;
                rowVm.Cells.Add(new ConfusionMatrixCellViewModel
                {
                    Score = score,
                    DisplayText = score <= 0.01 ? string.Empty : score.ToString("0.0", CultureInfo.InvariantCulture),
                    Background = BuildHeatBrush(normalized),
                    Foreground = normalized > 0.65 ? Brushes.White : Brushes.LightGray
                });
            }

            Rows.Add(rowVm);
        }

        SummaryText = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.0} weighted observations across {1} symbols (half-life: {2:0} days).",
            totalScore,
            symbols.Length,
            _halfLifeDays);
    }

    private static double CalculateScore(ConfusionObservation observation, DateTimeOffset now, double halfLifeDays)
    {
        if (observation.Count <= 0)
        {
            return 0;
        }

        var ageDays = Math.Max(0, (now - observation.RecordedAt).TotalDays);
        // Half-life decay: weight halves every halfLifeDays.
        var decay = Math.Pow(2.0, -ageDays / halfLifeDays);
        var distanceFactor = 1d / Math.Max(1, observation.Distance);
        return observation.Count * decay * distanceFactor;
    }

    private static IBrush BuildHeatBrush(double normalized)
    {
        var clamped = Math.Clamp(normalized, 0, 1);
        var baseColor = Color.Parse("#0B1020");
        var hotColor = Color.Parse("#DC2626");

        byte Blend(byte from, byte to)
        {
            return (byte)(from + (to - from) * clamped);
        }

        var color = Color.FromRgb(
            Blend(baseColor.R, hotColor.R),
            Blend(baseColor.G, hotColor.G),
            Blend(baseColor.B, hotColor.B));

        return new SolidColorBrush(color);
    }
}

public sealed class ConfusionMatrixRowViewModel
{
    public string ExpectedSymbol { get; init; } = string.Empty;

    public ObservableCollection<ConfusionMatrixCellViewModel> Cells { get; } = [];
}

public sealed class ConfusionMatrixCellViewModel
{
    public double Score { get; init; }

    public string DisplayText { get; init; } = string.Empty;

    public IBrush Background { get; init; } = Brushes.Transparent;

    public IBrush Foreground { get; init; } = Brushes.White;
}
