using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Redux_SDK_Manager.Converters;

/// <summary>
/// True when the bound int equals the converter parameter. Used one-way to light up the active
/// top-bar tab (the RadioButton's Command writes the new tab index back).
/// </summary>
public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int current && parameter is not null && int.TryParse(parameter.ToString(), out var target)
            && current == target;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
