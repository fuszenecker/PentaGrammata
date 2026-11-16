using CommunityToolkit.Mvvm.ComponentModel;
using PentaGrammata.Configuration;

namespace PentaGrammata.ViewModels;

/// <summary>
/// The editable repetition range of one practice topic in the settings dialog. Raising the
/// minimum above the maximum raises the maximum too, and the other way round, so the range
/// always stays valid.
/// </summary>
public sealed partial class RepetitionViewModel : ObservableObject
{
    public RepetitionViewModel(Repetition? repetition)
    {
        minimum = repetition?.Minimum ?? 1;
        maximum = repetition?.Maximum ?? minimum;
    }

    /// <summary>The highest count the editors offer.</summary>
    public int Limit => Repetition.Limit;

    [ObservableProperty]
    private int minimum;

    [ObservableProperty]
    private int maximum;

    partial void OnMinimumChanged(int value)
    {
        if (Maximum < value)
            Maximum = value;
    }

    partial void OnMaximumChanged(int value)
    {
        if (Minimum > value)
            Minimum = value;
    }

    public Repetition ToRepetition() => new(Minimum, Maximum);
}
