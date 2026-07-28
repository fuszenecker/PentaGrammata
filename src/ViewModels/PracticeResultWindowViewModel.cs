using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;

namespace PentaGrammata.ViewModels;

public sealed class PracticeResultWindowViewModel : ViewModelBase
{
    private readonly IPracticeResultStatisticsStore _statisticsStore;
    private readonly IInfoDialogService _infoDialogService;
    private readonly PracticeResultStatisticsRecord _record;
    private bool _isSaving;
    private bool _isSaveCompleted;

    public ObservableCollection<PracticeResultRowViewModel> Rows { get; }

    public string CharacterCountText { get; }
    public string ErrorsText { get; }
    public string ErrorRateText { get; }
    public StatusLevel ResultStatus { get; }
    public IAsyncRelayCommand SaveResultsCommand { get; }

    public bool IsSaveCompleted
    {
        get => _isSaveCompleted;
        private set
        {
            if (SetProperty(ref _isSaveCompleted, value))
            {
                SaveResultsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                SaveResultsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PracticeResultWindowViewModel(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise,
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService)
    {
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;
        _isSaveCompleted = alreadySaved;

        Rows = new ObservableCollection<PracticeResultRowViewModel>(
            result.Rows.Select(row => new PracticeResultRowViewModel
            {
                SentGroup = row.SentGroup.ToUpperInvariant(),
                ReceivedGroup = row.ReceivedGroup.ToUpperInvariant(),
                DifferenceSegments = ParseDifferenceSegments(row.Difference)
            }));

        CharacterCountText = result.CharacterCount.ToString(CultureInfo.InvariantCulture);
        ErrorsText = result.ErrorCount.ToString(CultureInfo.InvariantCulture);
        ErrorRateText = $"{result.ErrorRatePercent:F2}%";
        ResultStatus = result.IsSuccessful ? StatusLevel.Success : StatusLevel.Error;
        var recordedAt = DateTimeOffset.Now;

        _record = new PracticeResultStatisticsRecord
        {
            RecordedAt = recordedAt,
            CharacterWpm = characterWpm,
            AverageWpm = averageWpm,
            CharacterCount = result.CharacterCount,
            ErrorCount = result.ErrorCount,
            ErrorRatePercent = result.ErrorRatePercent,
            ErrorThresholdPercent = errorThresholdPercent,
            NoiseType = noise.Type,
            NoiseLevelDb = noise.LevelDb,
            NoiseBandwidthHz = noise.BandwidthHz,
            AgcEnabled = noise.AgcEnabled,
            AgcDelaySeconds = noise.AgcDelaySeconds,
            ApfEnabled = noise.ApfEnabled,
            ApfBandwidthHz = noise.ApfBandwidthHz,
            ApfPeakGainDb = noise.ApfPeakGainDb,
            Confusions = LevenshteinConfusionExtractor.Extract(result.Rows, recordedAt)
        };

        SaveResultsCommand = new AsyncRelayCommand(SaveResultsAsync, CanSaveResults);
    }

    private bool CanSaveResults()
    {
        return !IsSaving && !IsSaveCompleted;
    }

    private async Task SaveResultsAsync()
    {
        if (!CanSaveResults())
        {
            return;
        }

        IsSaving = true;
        try
        {
            await _statisticsStore.SaveAsync(_record);
            IsSaveCompleted = true;
            await _infoDialogService.ShowInfoAsync(
                "Results saved",
                $"Statistics were saved to:\n{_statisticsStore.DatabasePath}",
                "ResultsSaved",
                detailHeading: "Database location");
        }
        catch (StatisticsStoreException ex)
        {
            await ShowSaveFailedAsync(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private Task ShowSaveFailedAsync(StatisticsStoreException ex)
    {
        // Surface the underlying cause (e.g. "Database is locked") rather than the
        // generic wrapper message, without leaking the storage exception type.
        var detail = ex.InnerException?.Message ?? ex.Message;
        return _infoDialogService.ShowInfoAsync(
            "Save failed",
            $"Could not save statistics:\n{detail}");
    }

    private static ObservableCollection<PracticeResultDiffSegmentViewModel> ParseDifferenceSegments(string difference)
    {
        var segments = new ObservableCollection<PracticeResultDiffSegmentViewModel>();
        if (string.IsNullOrEmpty(difference))
        {
            return segments;
        }

        var i = 0;
        while (i < difference.Length)
        {
            if (difference[i] == '[' && i + 2 < difference.Length && (difference[i + 1] == '+' || difference[i + 1] == '-'))
            {
                var end = difference.IndexOf(']', i + 2);
                if (end > i)
                {
                    var tokenContent = difference.Substring(i + 2, end - (i + 2));
                    segments.Add(new PracticeResultDiffSegmentViewModel
                    {
                        Text = tokenContent,
                        Kind = difference[i + 1] == '+' ? DiffSegmentKind.Inserted : DiffSegmentKind.Deleted
                    });
                    i = end + 1;
                    continue;
                }
            }

            var ch = difference[i];
            segments.Add(new PracticeResultDiffSegmentViewModel
            {
                Text = ch.ToString(),
                Kind = ch switch
                {
                    '.' => DiffSegmentKind.Unchanged,
                    ' ' => DiffSegmentKind.Unchanged,
                    _ => DiffSegmentKind.Substituted
                }
            });
            i++;
        }

        return segments;
    }
}

public sealed class PracticeResultRowViewModel
{
    public string SentGroup { get; init; } = string.Empty;
    public string ReceivedGroup { get; init; } = string.Empty;
    public ObservableCollection<PracticeResultDiffSegmentViewModel> DifferenceSegments { get; init; } = [];
}

public sealed class PracticeResultDiffSegmentViewModel
{
    public string Text { get; init; } = string.Empty;
    public DiffSegmentKind Kind { get; init; } = DiffSegmentKind.Unchanged;
}
