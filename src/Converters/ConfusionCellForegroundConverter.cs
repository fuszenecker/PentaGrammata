using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PentaGrammata.Converters;

/// <summary>
/// Cell text color for the confusion matrix: white on deep heat fills (normalized
/// score above the threshold where the composed red gets dark), light gray otherwise.
/// </summary>
public sealed class ConfusionCellForegroundConverter : IValueConverter
{
    private const double LightTextThreshold = 0.65;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double normalized && normalized > LightTextThreshold
            ? Brushes.White
            : Brushes.LightGray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}