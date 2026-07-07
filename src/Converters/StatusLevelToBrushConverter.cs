using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Converters;

/// <summary>
/// Maps a toolkit-neutral <see cref="StatusLevel"/> from a view model onto the
/// concrete brush used by the view. Keeps color decisions in the view layer.
/// </summary>
public sealed class StatusLevelToBrushConverter : IValueConverter
{
    public static readonly StatusLevelToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is StatusLevel status ? status : StatusLevel.Neutral;
        return level switch
        {
            StatusLevel.Info => Brushes.CornflowerBlue,
            StatusLevel.Success => Brushes.LimeGreen,
            StatusLevel.Error => Brushes.IndianRed,
            _ => Brushes.Gainsboro,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
