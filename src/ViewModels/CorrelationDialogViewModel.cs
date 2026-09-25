using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class CorrelationDialogViewModel : ViewModelBase
{
    // The analysis window is how many days back the scatter reaches. Default and bounds for
    // the user-facing control.
    private const double DefaultWindowDays = 10d;
    private const double MinWindowDaysValue = 1d;
    private const double MaxWindowDaysValue = 365d;

    private readonly IPracticeResultStatisticsService _statisticsService;
    private readonly IConfigurationService _configurationService;
    private readonly ICorrelationAnalysisService _analysisService;
    private string _summaryText = "Loading correlation data...";
    private double _windowDays = DefaultWindowDays;
    private bool _windowDaysDirty;
    private SpeedErrorCorrelation _correlation = new();

    // Cached so changing the window recomputes the scatter without re-querying.
    private IReadOnlyList<PracticeResultStatisticsRecord> _records = [];

    public event Action? CloseRequested;

    public IAsyncRelayCommand CloseCommand { get; }

    public double MinWindowDays => MinWindowDaysValue;

    public double MaxWindowDays => MaxWindowDaysValue;

    /// <summary>
    /// Analysis window in days: only sessions recorded within this many days of now enter the
    /// scatter and the fit. Setting it recomputes from the already-loaded records.
    /// </summary>
    public double WindowDays
    {
        get => _windowDays;
        set
        {
            var clamped = Math.Clamp(value, MinWindowDaysValue, MaxWindowDaysValue);
            if (SetProperty(ref _windowDays, clamped))
            {
                _configurationService.SetCorrelationWindowDays(clamped);
                _windowDaysDirty = true;
                Rebuild();
            }
        }
    }

    /// <summary>The scatter and its fitted line, rendered by the correlation chart.</summary>
    public SpeedErrorCorrelation Correlation
    {
        get => _correlation;
        private set => SetProperty(ref _correlation, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public CorrelationDialogViewModel(
        IPracticeResultStatisticsService statisticsService,
        IConfigurationService configurationService,
        ICorrelationAnalysisService analysisService)
    {
        _statisticsService = statisticsService;
        _configurationService = configurationService;
        _analysisService = analysisService;
        var configuredWindow = Math.Clamp(
            _configurationService.Current.Analytics.CorrelationWindowDays,
            MinWindowDaysValue,
            MaxWindowDaysValue);
        _windowDays = configuredWindow;

        // Persist the clamped value if the configured window was out of range, so the on-disk
        // configuration stays consistent with what the user sees.
        if (configuredWindow != _configurationService.Current.Analytics.CorrelationWindowDays)
        {
            _configurationService.SetCorrelationWindowDays(configuredWindow);
        }

        CloseCommand = new AsyncRelayCommand(CloseAsync);
    }

    public async Task InitializeAsync()
    {
        _records = await _statisticsService.GetStatisticsRecordsAsync().ConfigureAwait(true);
        Rebuild();
    }

    private void Rebuild()
    {
        var correlation = _analysisService.Analyze(_records, _windowDays, DateTimeOffset.UtcNow);
        Correlation = correlation;
        SummaryText = BuildSummary(correlation, _windowDays);
    }

    /// <summary>
    /// One line describing the window, the strength and direction of the correlation and the
    /// slope of the fit. Falls back to an explanatory message when there is nothing to fit,
    /// because an empty plot with no caption reads as a bug rather than as missing data.
    /// </summary>
    private static string BuildSummary(SpeedErrorCorrelation correlation, double windowDays)
    {
        var sessions = correlation.Points.Count;
        var window = string.Format(
            CultureInfo.InvariantCulture,
            "{0} session(s) in the last {1:0} day(s)",
            sessions,
            windowDays);

        if (sessions == 0)
        {
            return $"{window}. Nothing to correlate yet.";
        }

        if (!correlation.HasFit)
        {
            return sessions < 2
                ? $"{window}. At least two sessions are needed for a correlation."
                : $"{window}. No spread to correlate: every session ran at the same speed or scored the same error rate.";
        }

        var summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0}. Correlation r = {1:0.00} ({2}), R² = {3:0.00}. Fit: error = {4:0.00} % {5} {6:0.000} %/WPM.",
            window,
            correlation.PearsonR,
            DescribeStrength(correlation.PearsonR),
            correlation.PearsonR * correlation.PearsonR,
            correlation.Intercept,
            correlation.Slope < 0 ? "-" : "+",
            Math.Abs(correlation.Slope));

        if (correlation.ResidualStandardDeviation <= 0)
        {
            return summary;
        }

        return summary + string.Format(
            CultureInfo.InvariantCulture,
            " Band: ±1 σ = {0:0.00} percentage points around the line.",
            correlation.ResidualStandardDeviation);
    }

    private static string DescribeStrength(double r)
    {
        var direction = r < 0 ? "negative" : "positive";
        var magnitude = Math.Abs(r);
        var strength = magnitude switch
        {
            < 0.2 => "negligible",
            < 0.4 => "weak",
            < 0.6 => "moderate",
            < 0.8 => "strong",
            _ => "very strong",
        };

        return strength == "negligible" ? strength : $"{strength} {direction}";
    }

    public void OnDialogClosed()
    {
        // Reached when the window closes without the CloseCommand (e.g. the title-bar X).
        // CloseAsync handles the awaited path; here a fire-and-forget save is enough because
        // the process flushes on exit.
        if (TryConsumeWindowDaysDirty())
        {
            _configurationService.RequestSave();
        }
    }

    private async Task CloseAsync()
    {
        if (TryConsumeWindowDaysDirty())
        {
            await _configurationService.SaveAsync().ConfigureAwait(true);
        }

        CloseRequested?.Invoke();
    }

    /// <summary>
    /// If a window change is pending, marks it consumed and returns true so the caller can
    /// persist. Centralizes the dirty flag so the awaited (CloseCommand) and fire-and-forget
    /// (window-closed) paths can never both save or both skip.
    /// </summary>
    private bool TryConsumeWindowDaysDirty()
    {
        if (!_windowDaysDirty)
        {
            return false;
        }

        _windowDaysDirty = false;
        return true;
    }
}
