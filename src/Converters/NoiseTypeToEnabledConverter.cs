using System;
using System.Globalization;
using Avalonia.Data.Converters;
using PentaGrammata.Configuration;

namespace PentaGrammata.Converters;

/// <summary>
/// True when a <see cref="NoiseType"/> is anything other than <see cref="NoiseType.None"/>.
/// Used to disable the noise level/bandwidth inputs when noise is switched off.
/// </summary>
public sealed class NoiseTypeToEnabledConverter : IValueConverter
{
    public static readonly NoiseTypeToEnabledConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is NoiseType type && type != NoiseType.None;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
