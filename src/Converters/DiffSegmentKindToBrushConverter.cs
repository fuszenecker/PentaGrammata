using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Converters;

/// <summary>
/// Maps a toolkit-neutral <see cref="DiffSegmentKind"/> onto the concrete brush
/// used to render a difference segment. Keeps color decisions in the view layer.
/// </summary>
public sealed class DiffSegmentKindToBrushConverter : IValueConverter
{
    public static readonly DiffSegmentKindToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value is DiffSegmentKind segmentKind ? segmentKind : DiffSegmentKind.Unchanged;
        return kind switch
        {
            DiffSegmentKind.Inserted => Brushes.LimeGreen,
            DiffSegmentKind.Deleted => Brushes.IndianRed,
            DiffSegmentKind.Substituted => Brushes.Gold,
            _ => Brushes.Gainsboro,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
