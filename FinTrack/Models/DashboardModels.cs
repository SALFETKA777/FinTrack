using System;

namespace FinTrack.Models;

/// <summary>
/// Итоги по одному месяцу для графика Dashboard.
/// Не является сущностью базы данных — только данные для отображения.
/// </summary>
public sealed class MonthlySummary
{
    /// <summary>Первый день месяца, к которому относятся суммы.</summary>
    public DateTime Month { get; init; }

    /// <summary>Сумма доходов за месяц.</summary>
    public decimal Income { get; init; }

    /// <summary>Сумма расходов за месяц.</summary>
    public decimal Expense { get; init; }

    /// <summary>Баланс месяца: доходы минус расходы.</summary>
    public decimal Balance => Income - Expense;
}

/// <summary>
/// Доля одной категории в расходах за выбранный месяц.
/// Не является сущностью базы данных — только данные для диаграммы.
/// </summary>
public sealed class CategoryShare
{
    /// <summary>Название категории (или «Другое» для объединённых).</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Цвет категории в формате #RRGGBB.</summary>
    public string Color { get; init; } = "#808080";

    /// <summary>Сумма расходов категории за месяц.</summary>
    public decimal Amount { get; init; }

    /// <summary>Доля в общих расходах месяца: значение от 0 до 1.</summary>
    public decimal Share { get; init; }
}
