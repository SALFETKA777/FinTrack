using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinTrack.Models;

namespace FinTrack.Converters;

/// <summary>
/// Превращает <see cref="TransactionType"/> в кисть:
/// доход — зелёный, расход — красный. Кисти берутся из палитры App.axaml.
/// </summary>
public sealed class TransactionTypeToBrushConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        string resourceKey = value is TransactionType type
                             && type == TransactionType.Income
            ? "FtIncomeBrush"
            : "FtExpenseBrush";

        if (Application.Current is { } app
            && app.TryFindResource(resourceKey, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        // Запасной вариант, если ресурс почему-то недоступен.
        return Brushes.White;
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
