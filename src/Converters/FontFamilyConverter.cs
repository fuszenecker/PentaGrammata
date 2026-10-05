using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PentaGrammata.Converters;

public sealed class FontFamilyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name && !string.IsNullOrWhiteSpace(name)
            ? new FontFamily(name)
            : FontFamily.Default;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Font family conversion is one-way.");
}
