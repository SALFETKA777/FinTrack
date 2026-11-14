using System;
using System.Collections.Generic;
using System.Linq;
using FinTrack.Models;

namespace FinTrack.Services;

/// <summary>
/// Расчёты для Dashboard. Сервис работает только с уже загруженными транзакциями
/// и не обращается к базе данных — это позволяет переключать месяцы без запросов к SQLite.
/// </summary>
public class StatisticsService
{
    private const string OtherCategoryName = "Другое";
    private const string OtherCategoryColor = "#6B7280";
    private const string UnknownCategoryName = "Без категории";
    private const string UnknownCategoryColor = "#808080";

    /// <summary>
    /// Общий баланс за всё время: все доходы минус все расходы.
    /// </summary>
    public decimal CalculateTotalBalance(IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return CalculateIncome(transactions) - CalculateExpense(transactions);
    }

    /// <summary>
    /// Сумма доходов за указанный месяц.
    /// </summary>
    public decimal CalculateMonthIncome(
        IEnumerable<Transaction> transactions,
        DateTime month)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return CalculateIncome(FilterByMonth(transactions, month));
    }

    /// <summary>
    /// Сумма расходов за указанный месяц.
    /// </summary>
    public decimal CalculateMonthExpense(
        IEnumerable<Transaction> transactions,
        DateTime month)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return CalculateExpense(FilterByMonth(transactions, month));
    }

    /// <summary>
    /// Готовит данные для графика за несколько месяцев (по умолчанию 6).
    /// Месяцы без операций включаются с нулевыми суммами.
    /// </summary>
    /// <param name="endMonth">Последний месяц графика.</param>
    /// <param name="monthsCount">Сколько месяцев показать.</param>
    /// <returns>Список от самого старого месяца к самому новому.</returns>
    public IReadOnlyList<MonthlySummary> BuildMonthlySummaries(
        IEnumerable<Transaction> transactions,
        DateTime endMonth,
        int monthsCount = 6)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        if (monthsCount <= 0)
        {
            return Array.Empty<MonthlySummary>();
        }

        // Материализуем один раз, чтобы не перебирать ленивую последовательность на каждом месяце.
        var materialized = transactions as IReadOnlyCollection<Transaction>
                           ?? transactions.ToList();

        var lastMonth = StartOfMonth(endMonth);
        var result = new List<MonthlySummary>(monthsCount);

        for (int offset = monthsCount - 1; offset >= 0; offset--)
        {
            DateTime month = lastMonth.AddMonths(-offset);

            decimal income = 0m;
            decimal expense = 0m;

            foreach (var transaction in materialized)
            {
                if (transaction.Date.Year != month.Year ||
                    transaction.Date.Month != month.Month)
                {
                    continue;
                }

                if (transaction.Type == TransactionType.Income)
                {
                    income += transaction.Amount;
                }
                else if (transaction.Type == TransactionType.Expense)
                {
                    expense += transaction.Amount;
                }
            }

            result.Add(new MonthlySummary
            {
                Month = month,
                Income = income,
                Expense = expense
            });
        }

        return result;
    }

    /// <summary>
    /// Распределяет расходы выбранного месяца по категориям.
    /// Возвращает топ-категорий и объединяет остальные в «Другое».
    /// </summary>
    /// <param name="topCount">Сколько категорий показать отдельно.</param>
    /// <returns>Пустой список, если расходов за месяц нет.</returns>
    public IReadOnlyList<CategoryShare> BuildCategoryShares(
        IEnumerable<Transaction> transactions,
        DateTime month,
        int topCount = 6)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var grouped = FilterByMonth(transactions, month)
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.CategoryId)
            .Select(group =>
            {
                var category = group.First().Category;

                string name = string.IsNullOrWhiteSpace(category?.Name)
                    ? UnknownCategoryName
                    : category!.Name;

                string color = string.IsNullOrWhiteSpace(category?.Color)
                    ? UnknownCategoryColor
                    : category!.Color;

                return (Name: name, Color: color, Amount: group.Sum(t => t.Amount));
            })
            .OrderByDescending(item => item.Amount)
            .ToList();

        decimal total = grouped.Sum(item => item.Amount);

        // Нет расходов или нулевая сумма — доля не определена, деления на ноль не допускаем.
        if (grouped.Count == 0 || total <= 0m)
        {
            return Array.Empty<CategoryShare>();
        }

        int categoriesToShow = Math.Clamp(topCount, 1, grouped.Count);
        var result = new List<CategoryShare>(categoriesToShow + 1);

        for (int i = 0; i < categoriesToShow; i++)
        {
            var item = grouped[i];

            result.Add(new CategoryShare
            {
                CategoryName = item.Name,
                Color = item.Color,
                Amount = item.Amount,
                Share = item.Amount / total
            });
        }

        if (grouped.Count > categoriesToShow)
        {
            decimal otherAmount = 0m;

            for (int i = categoriesToShow; i < grouped.Count; i++)
            {
                otherAmount += grouped[i].Amount;
            }

            result.Add(new CategoryShare
            {
                CategoryName = OtherCategoryName,
                Color = OtherCategoryColor,
                Amount = otherAmount,
                Share = otherAmount / total
            });
        }

        return result;
    }

    /// <summary>
    /// Возвращает самые новые транзакции (по дате, затем по Id).
    /// </summary>
    public IReadOnlyList<Transaction> GetRecentTransactions(
        IEnumerable<Transaction> transactions,
        int count = 5)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        if (count <= 0)
        {
            return Array.Empty<Transaction>();
        }

        return transactions
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(count)
            .ToList();
    }

    private static decimal CalculateIncome(IEnumerable<Transaction> transactions)
    {
        decimal income = 0m;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Income)
            {
                income += transaction.Amount;
            }
        }

        return income;
    }

    private static decimal CalculateExpense(IEnumerable<Transaction> transactions)
    {
        decimal expense = 0m;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Expense)
            {
                expense += transaction.Amount;
            }
        }

        return expense;
    }

    private static IEnumerable<Transaction> FilterByMonth(
        IEnumerable<Transaction> transactions,
        DateTime month)
    {
        return transactions.Where(t =>
            t.Date.Year == month.Year && t.Date.Month == month.Month);
    }

    private static DateTime StartOfMonth(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1);
    }
}
