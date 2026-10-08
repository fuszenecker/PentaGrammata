using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Presentation;
using PentaGrammata.Exceptions;

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
    private readonly IUiDispatcher _uiDispatcher;
    private CancellationTokenSource? _rebuildCancellation;
    private bool _hasPracticeConfusions;
    private string _summaryText = "Loading confusion matrix...";
    private double _halfLifeDays = DefaultHalfLifeDays;
    private bool _halfLifeDirty;

    // Cached so adjusting the half-life recomputes the matrix without re-querying.
    private IReadOnlyList<ConfusionObservation> _observations = [];

    public event Action? CloseRequested;

    public IAsyncRelayCommand CloseCommand { get; }
    public IAsyncRelayCommand PracticeConfusionsCommand { get; }

    public ObservableCollection<ConfusionMatrixHeaderViewModel> ColumnHeaders { get; } = [];

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
                ScheduleRebuild();
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
        IConfusionAnalysisService analysisService,
        IUiDispatcher uiDispatcher)
    {
        _statisticsService = statisticsService;
        _configurationService = configurationService;
        _analysisService = analysisService;
        _uiDispatcher = uiDispatcher;
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
        await RebuildAsync();
    }

    private void ScheduleRebuild() => _ = RebuildAsync();

    private async Task RebuildAsync()
    {
        _rebuildCancellation?.Cancel();
        _rebuildCancellation?.Dispose();
        var cts = _rebuildCancellation = new CancellationTokenSource();
        var halfLife = _halfLifeDays;
        var observations = _observations;
        var now = DateTimeOffset.UtcNow;
        try
        {
        var prepared = await Task.Run(() => (
            _analysisService.BuildMatrix(observations, halfLife, now),
            _analysisService.WeightedSymbolCounts(observations, halfLife, now).Count > 0), cts.Token);
        await _uiDispatcher.InvokeAsync(() =>
        {
        if (cts.IsCancellationRequested) return;
        _hasPracticeConfusions = prepared.Item2;
        ColumnHeaders.Clear();
        Rows.Clear();

        var result = prepared.Item1;

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
        for (var column = 0; column < matrix.Symbols.Count; column++)
        {
            ColumnHeaders.Add(new ConfusionMatrixHeaderViewModel
            {
                Symbol = matrix.Symbols[column],
                IsEvenColumn = column % 2 == 0
            });
        }

        for (var row = 0; row < matrix.Symbols.Count; row++)
        {
            var isEvenRow = row % 2 == 0;
            var rowVm = new ConfusionMatrixRowViewModel
            {
                ExpectedSymbol = matrix.Symbols[row],
                IsEvenRow = isEvenRow
            };

            for (var column = 0; column < matrix.Symbols.Count; column++)
            {
                var score = matrix.Cells[row, column];
                var normalized = matrix.MaxScore > 0 ? score / matrix.MaxScore : 0;
                rowVm.Cells.Add(new ConfusionMatrixCellViewModel
                {
                    Score = score,
                    NormalizedScore = normalized,
                    IsEvenRow = isEvenRow,
                    IsEvenColumn = column % 2 == 0,
                    DisplayText = score <= 0.01 ? string.Empty : score.ToString("0.0", CultureInfo.InvariantCulture)
                });
            }

            Rows.Add(rowVm);
        }

        SummaryText = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.0} weighted observations across {1} symbols (half-life: {2:0} days).",
            matrix.TotalScore,
            matrix.Symbols.Count,
            halfLife);
        PracticeConfusionsCommand.NotifyCanExecuteChanged();
        });
        }
        catch (OperationCanceledException) { }
    }

    private bool CanCreatePracticeConfusions()
    {
        return _hasPracticeConfusions;
    }

    private async Task CreatePracticeConfusionsAsync()
    {
        var observations = _observations;
        var halfLifeDays = _halfLifeDays;
        var now = DateTimeOffset.UtcNow;
        var characterSet = await Task.Run(() => _analysisService.BuildPracticeConfusionsCharacterSet(
            observations, halfLifeDays, now, PracticeSetTargetSymbolCount));
        if (string.IsNullOrWhiteSpace(characterSet))
        {
            return;
        }

        try
        {
            await _configurationService.UpsertCharacterSetAndSelectAsync(PracticeConfusionsSetName, characterSet);
        }
        catch (ConfigurationPersistenceException ex)
        {
            _halfLifeDirty = true;
            SummaryText = $"Could not save settings: {ex.InnerException?.Message ?? ex.Message}";
            return;
        }
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
            try
            {
                await _configurationService.SaveAsync();
            }
            catch (ConfigurationPersistenceException ex)
            {
                _halfLifeDirty = true;
                SummaryText = $"Could not save settings: {ex.InnerException?.Message ?? ex.Message}";
                return;
            }
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

}

public sealed class ConfusionMatrixHeaderViewModel
{
    public string Symbol { get; init; } = string.Empty;

    public bool IsEvenColumn { get; init; }
}

public sealed class ConfusionMatrixRowViewModel
{
    public string ExpectedSymbol { get; init; } = string.Empty;

    public bool IsEvenRow { get; init; }

    public ObservableCollection<ConfusionMatrixCellViewModel> Cells { get; } = [];
}

public sealed class ConfusionMatrixCellViewModel
{
    public double Score { get; init; }

    /// <summary>Score relative to the matrix maximum; drives the heat fill via the background converter.</summary>
    public double NormalizedScore { get; init; }

    public bool IsEvenRow { get; init; }

    public bool IsEvenColumn { get; init; }

    public string DisplayText { get; init; } = string.Empty;
}
