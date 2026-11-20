using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Presentation;
using PentaGrammata.Exceptions;

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
    private readonly IUiDispatcher _uiDispatcher;
    private CancellationTokenSource? _rebuildCancellation;
    private string _statusText = string.Empty;
    private double _windowDays = DefaultWindowDays;
    private bool _windowDaysDirty;

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
                _ = RebuildAsync();
            }
        }
    }

    /// <summary>
    /// The error threshold currently configured for practice, in percent, drawn as a horizontal
    /// line on the chart. Read once when the dialog opens: the settings cannot be changed while
    /// it is up, and the point is to show what the next session will be judged against, not what
    /// each past session was.
    /// </summary>
    public double ErrorThresholdPercent { get; }

    /// <summary>Error rate against average speed.</summary>
    public CorrelationTabViewModel Speed { get; } = new(CorrelationAxis.AverageSpeed, "Speed");

    /// <summary>Error rate against signal-to-noise ratio, over the sessions that had noise.</summary>
    public CorrelationTabViewModel SignalToNoise { get; } = new(CorrelationAxis.SignalToNoise, "Signal-to-noise");

    /// <summary>Error rate against QSB fade depth, over the sessions that had QSB.</summary>
    public CorrelationTabViewModel QsbDepth { get; } = new(CorrelationAxis.QsbDepth, "QSB depth");

    /// <summary>The tabs, in the order the dialog shows them.</summary>
    public IReadOnlyList<CorrelationTabViewModel> Tabs => [Speed, SignalToNoise, QsbDepth];

    /// <summary>A message about the dialog itself rather than a tab (a failed save); empty otherwise.</summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_statusText);

    public CorrelationDialogViewModel(
        IPracticeResultStatisticsService statisticsService,
        IConfigurationService configurationService,
        ICorrelationAnalysisService analysisService,
        IUiDispatcher uiDispatcher)
    {
        _statisticsService = statisticsService;
        _configurationService = configurationService;
        _analysisService = analysisService;
        _uiDispatcher = uiDispatcher;
        var configuredWindow = Math.Clamp(
            _configurationService.Current.Analytics.CorrelationWindowDays,
            MinWindowDaysValue,
            MaxWindowDaysValue);
        _windowDays = configuredWindow;
        ErrorThresholdPercent = _configurationService.Current.Practice.ErrorThreshold;

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
        await RebuildAsync();
    }

    private async Task RebuildAsync()
    {
        _rebuildCancellation?.Cancel();
        _rebuildCancellation?.Dispose();
        var cts = _rebuildCancellation = new CancellationTokenSource();
        var records = _records;
        var windowDays = _windowDays;
        var now = DateTimeOffset.UtcNow;
        try
        {
            var tabs = Tabs;
            var correlations = await Task.Run(
                () => tabs.Select(tab => _analysisService.Analyze(records, tab.Axis, windowDays, now)).ToArray(),
                cts.Token);
            await _uiDispatcher.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested) return;
                for (var i = 0; i < tabs.Count; i++)
                {
                    tabs[i].Show(correlations[i], windowDays);
                }
            });
        }
        catch (OperationCanceledException) { }
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
            try
            {
                await _configurationService.SaveAsync().ConfigureAwait(true);
            }
            catch (ConfigurationPersistenceException ex)
            {
                _windowDaysDirty = true;
                StatusText = $"Could not save settings: {ex.InnerException?.Message ?? ex.Message}";
                return;
            }
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
