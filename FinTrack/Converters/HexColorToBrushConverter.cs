using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FinTrack.Converters;

/// <summary>
/// Превращает строку цвета категории (например, "#F59E0B") в кисть.
/// Если строка пустая или некорректная, возвращает нейтральный серый.
/// </summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    private static readonly IBrush FallbackBrush =
        new SolidColorBrush(Color.Parse("#808080"));

    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is string text && Color.TryParse(text, out var color))
        {
            return new SolidColorBrush(color);
        }

        return FallbackBrush;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
