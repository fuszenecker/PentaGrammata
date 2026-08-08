using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    private const string PracticeConfusionsSetName = "Practice confusions";
    private const int PracticeSetTargetSymbolCount = 200;

    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IConfigurationService _configurationService;
    private readonly IConfusionAnalysisService _analysisService;
    private string _summaryText = "Loading confusion matrix...";
    private double _halfLifeDays = DefaultHalfLifeDays;
    private bool _halfLifeDirty;

    // Cached so adjusting the half-life recomputes the matrix without re-querying.
    private IReadOnlyList<ConfusionObservation> _observations = [];

    public event Action? CloseRequested;

    public IAsyncRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand PracticeConfusionsCommand { get; }

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
                _configurationService.SetConfusionsHalfLife(clamped);
                _halfLifeDirty = true;
                Rebuild();
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public ConfusionsDialogViewModel(
        IPracticeResultStatisticsService statisticsService,
        IConfigurationService configurationService,
        IConfusionAnalysisService analysisService)
    {
        _statisticsService = statisticsService;
        _configurationService = configurationService;
        _analysisService = analysisService;
        var configuredHalfLife = Math.Clamp(
            _configurationService.Current.Analytics.ConfusionsHalfLifeDays,
            MinHalfLifeDays,
            MaxHalfLifeDays);
        _halfLifeDays = configuredHalfLife;

        // Persist the clamped value if the configured half-life was out of range, so the
        // on-disk configuration stays consistent with what the user sees.
        if (configuredHalfLife != _configurationService.Current.Analytics.ConfusionsHalfLifeDays)
        {
            _configurationService.SetConfusionsHalfLife(configuredHalfLife);
        }

        CloseCommand = new AsyncRelayCommand(CloseAsync);
        PracticeConfusionsCommand = new AsyncRelayCommand(CreatePracticeConfusionsAsync, CanCreatePracticeConfusions);
    }

    public async Task InitializeAsync()
    {
        _observations = await _statisticsService.GetConfusionObservationsAsync();
        Rebuild();
    }

    private void Rebuild()
    {
        ColumnHeaders.Clear();
        Rows.Clear();

        var now = DateTimeOffset.UtcNow;
        var result = _analysisService.BuildMatrix(_observations, _halfLifeDays, now);

        switch (result.Status)
        {
            case ConfusionMatrixStatus.NoSubstitutionData:
                SummaryText = "No substitution confusion data yet.";
                PracticeConfusionsCommand.NotifyCanExecuteChanged();
                return;
            case ConfusionMatrixStatus.NoVisibleAfterWeighting:
                SummaryText = "No visible confusion data after weighting.";
                PracticeConfusionsCommand.NotifyCanExecuteChanged();
                return;
            case ConfusionMatrixStatus.NoVisibleAfterFiltering:
                SummaryText = "No visible confusion data after filtering.";
                PracticeConfusionsCommand.NotifyCanExecuteChanged();
                return;
        }

        var matrix = result.Matrix!;
        foreach (var symbol in matrix.Symbols)
        {
            ColumnHeaders.Add(symbol);
        }

        for (var row = 0; row < matrix.Symbols.Count; row++)
        {
            var rowVm = new ConfusionMatrixRowViewModel
            {
                ExpectedSymbol = matrix.Symbols[row]
            };

            for (var column = 0; column < matrix.Symbols.Count; column++)
            {
                var score = matrix.Cells[row, column];
                var normalized = score / matrix.MaxScore;
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
            matrix.TotalScore,
            matrix.Symbols.Count,
            _halfLifeDays);
        PracticeConfusionsCommand.NotifyCanExecuteChanged();
    }

    private bool CanCreatePracticeConfusions()
    {
        return _analysisService.WeightedSymbolCounts(_observations, _halfLifeDays, DateTimeOffset.UtcNow).Count > 0;
    }

    private async Task CreatePracticeConfusionsAsync()
    {
        var characterSet = _analysisService.BuildPracticeConfusionsCharacterSet(
            _observations, _halfLifeDays, DateTimeOffset.UtcNow, PracticeSetTargetSymbolCount);
        if (string.IsNullOrWhiteSpace(characterSet))
        {
            return;
        }

        await _configurationService.UpsertCharacterSetAndSelectAsync(PracticeConfusionsSetName, characterSet);
        // The upsert awaits a full SaveAsync, which also flushes any pending half-life
        // change, so nothing is left dirty.
        _halfLifeDirty = false;
        CloseRequested?.Invoke();
    }

    public void OnDialogClosed()
    {
        // Reached when the window closes without the CloseCommand (e.g. the title-bar X).
        // CloseAsync handles the awaited path; here a fire-and-forget save is enough because
        // the process flushes on exit. TryConsumeHalfLifeDirty ensures the flag is managed in
        // one place regardless of which close path runs.
        if (TryConsumeHalfLifeDirty())
        {
            _configurationService.RequestSave();
        }
    }

    private async Task CloseAsync()
    {
        if (TryConsumeHalfLifeDirty())
        {
            await _configurationService.SaveAsync();
        }

        CloseRequested?.Invoke();
    }

    /// <summary>
    /// If a half-life change is pending, marks it consumed and returns true so the caller can
    /// persist. Centralizes the dirty flag so the awaited (CloseCommand) and fire-and-forget
    /// (window-closed) paths can never both save or both skip.
    /// </summary>
    private bool TryConsumeHalfLifeDirty()
    {
        if (!_halfLifeDirty)
        {
            return false;
        }

        _halfLifeDirty = false;
        return true;
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
