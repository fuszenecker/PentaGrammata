using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PentaGrammata.Converters;

/// <summary>
/// Zebra tint for a header strip (column or row header) whose position on the other
/// axis is odd, so headers read as part of the band they lead: even parity gets the
/// barely-perceptible lift, odd parity the untouched base.
/// </summary>
public sealed class ZebraBackgroundConverter : IValueConverter
{
    private const string EvenTint = "#090D14";
    private const string OddBase = "#05080E";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new SolidColorBrush(value is true ? Color.Parse(EvenTint) : Color.Parse(OddBase));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}