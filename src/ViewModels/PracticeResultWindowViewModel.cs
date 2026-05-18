using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;

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
    public IBrush ResultForeground { get; }
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
        IPracticeResultStatisticsStore statisticsStore,
        IInfoDialogService infoDialogService)
    {
        _statisticsStore = statisticsStore;
        _infoDialogService = infoDialogService;

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
        ResultForeground = result.IsSuccessful ? Brushes.LimeGreen : Brushes.IndianRed;
        _record = new PracticeResultStatisticsRecord
        {
            RecordedAt = DateTimeOffset.Now,
            CharacterWpm = characterWpm,
            AverageWpm = averageWpm,
            CharacterCount = result.CharacterCount,
            ErrorCount = result.ErrorCount,
            ErrorRatePercent = result.ErrorRatePercent
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
                $"Statistics were saved to:\n{_statisticsStore.DatabasePath}");
        }
        catch (Exception ex)
        {
            await _infoDialogService.ShowInfoAsync(
                "Save failed",
                $"Could not save statistics:\n{ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
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
                        Foreground = difference[i + 1] == '+' ? Brushes.LimeGreen : Brushes.IndianRed
                    });
                    i = end + 1;
                    continue;
                }
            }

            var ch = difference[i];
            segments.Add(new PracticeResultDiffSegmentViewModel
            {
                Text = ch.ToString(),
                Foreground = ch switch
                {
                    '.' => Brushes.Gainsboro,
                    ' ' => Brushes.Gainsboro,
                    _ => Brushes.Gold
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
    public IBrush Foreground { get; init; } = Brushes.Gainsboro;
}
