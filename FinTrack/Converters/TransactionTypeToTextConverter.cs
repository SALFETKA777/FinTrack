using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FinTrack.Models;

namespace FinTrack.Converters;

/// <summary>
/// Превращает <see cref="TransactionType"/> в русскую подпись для интерфейса.
/// </summary>
public sealed class TransactionTypeToTextConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return value is TransactionType type
            ? type == TransactionType.Income ? "Доход" : "Расход"
            : string.Empty;
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
