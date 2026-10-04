using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PentaGrammata.Converters;

/// <summary>
/// Composes a confusion-matrix cell background: a faint zebra tint (barely-perceptible
/// lift on even axes so bands read as continuous rows/columns without fragmenting the
/// heat fill) with a red heat fill whose opacity scales with the normalized confusion
/// score. Values: [0] normalized score (0 renders the zebra tint alone), [1] is even
/// row, [2] is even column. The heat composes alpha-over onto the opaque zebra base, so
/// two overlapping dots do not add up to a brighter third shade.
/// </summary>
public sealed class ConfusionCellBackgroundConverter : IMultiValueConverter
{
    private const string ZebraBothEven = "#0B1019";
    private const string ZebraOneEven = "#090D14";
    private const string ZebraNoneEven = "#05080E";
    private const string Heat = "#DC2626";

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var heat = AsDouble(values, 0);
        var isEvenRow = AsBool(values, 1);
        var isEvenColumn = AsBool(values, 2);

        var zebraColor = (isEvenRow, isEvenColumn) switch
        {
            (true, true) => Color.Parse(ZebraBothEven),
            (true, false) => Color.Parse(ZebraOneEven),
            (false, true) => Color.Parse(ZebraOneEven),
            (false, false) => Color.Parse(ZebraNoneEven),
        };

        var clamped = Math.Clamp(heat, 0, 1);
        if (clamped <= 0)
        {
            return new SolidColorBrush(zebraColor);
        }

        var hotColor = Color.Parse(Heat);
        var composed = Color.FromRgb(
            (byte)(zebraColor.R + (hotColor.R - zebraColor.R) * clamped),
            (byte)(zebraColor.G + (hotColor.G - zebraColor.G) * clamped),
            (byte)(zebraColor.B + (hotColor.B - zebraColor.B) * clamped));

        return new SolidColorBrush(composed);
    }

    private static double AsDouble(IList<object?> values, int index)
        => values is { Count: > 0 } && index < values.Count && values[index] is double value ? value : 0;

    private static bool AsBool(IList<object?> values, int index)
        => values is { Count: > 0 } && index < values.Count && values[index] is bool value && value;
}