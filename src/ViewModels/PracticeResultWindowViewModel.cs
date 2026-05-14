using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;

using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class PracticeResultWindowViewModel : ViewModelBase
{
    public ObservableCollection<PracticeResultRowViewModel> Rows { get; }

    public string CharacterCountText { get; }
    public string ErrorsText { get; }
    public string ErrorRateText { get; }

    public PracticeResultWindowViewModel(PracticeResult result)
    {
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
