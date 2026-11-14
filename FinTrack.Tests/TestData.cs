using FinTrack.Models;

namespace FinTrack.Tests;

/// <summary>
/// Фабрики тестовых сущностей. Тесты работают только с объектами в памяти
/// и не обращаются к базе данных приложения.
/// </summary>
internal static class TestData
{
    public static Category CreateCategory(
        int id,
        string name = "Категория",
        string color = "#123456")
    {
        return new Category
        {
            Id = id,
            Name = name,
            Color = color
        };
    }

    public static Transaction CreateIncome(
        decimal amount,
        DateTime date,
        Category? category = null,
        int id = 0,
        string? description = null)
    {
        return CreateTransaction(amount, TransactionType.Income, date, category, id, description);
    }

    public static Transaction CreateExpense(
        decimal amount,
        DateTime date,
        Category? category = null,
        int id = 0,
        string? description = null)
    {
        return CreateTransaction(amount, TransactionType.Expense, date, category, id, description);
    }

    public static Transaction CreateTransaction(
        decimal amount,
        TransactionType type,
        DateTime date,
        Category? category = null,
        int id = 0,
        string? description = null)
    {
        return new Transaction
        {
            Id = id,
            Amount = amount,
            Type = type,
            Date = date,
            CategoryId = category?.Id ?? 0,
            Category = category,
            Description = description
        };
    }
}
