using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinTrack.Models;
using FinTrack.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinTrack.Tests;

/// <summary>
/// Интеграционные тесты TransactionService на временной SQLite-базе.
/// Реальная база пользователя не используется.
/// </summary>
public class TransactionServiceTests
{
    // ===================== Добавление =====================

    [Fact]
    public async Task AddAsync_PersistsTransactionAndAssignsId()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 125.50m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = new DateTime(2026, 11, 10),
            Description = "  Хлеб  "
        };

        var saved = await service.AddAsync(transaction);

        Assert.True(saved.Id > 0);
        Assert.Equal("Хлеб", saved.Description);
        Assert.Null(saved.Category);

        using var db = database.CreateContext();
        var stored = await db.Transactions
            .Include(item => item.Category)
            .SingleAsync();

        Assert.Equal(125.50m, stored.Amount);
        Assert.Equal(TransactionType.Expense, stored.Type);
        Assert.Equal(new DateTime(2026, 11, 10), stored.Date);
        Assert.Equal("Хлеб", stored.Description);
        Assert.Equal("Продукты", stored.Category!.Name);
    }

    [Fact]
    public async Task AddAsync_EmptyDescriptionBecomesNull()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var saved = await service.AddAsync(new Transaction
        {
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today,
            Description = "   "
        });

        Assert.Null(saved.Description);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    public async Task AddAsync_RejectsNonPositiveAmount(string amountText)
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture),
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.AddAsync(transaction));
    }

    [Fact]
    public async Task AddAsync_RejectsMoreThanTwoDecimalPlaces()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 10.123m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAsync(transaction));
    }

    [Fact]
    public async Task AddAsync_RejectsAmountAboveModelRange()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = TransactionRules.MaxAmount + 1m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.AddAsync(transaction));
    }

    [Fact]
    public async Task AddAsync_RejectsMissingCategory()
    {
        using var database = new SqliteTestDatabase();
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = 999,
            Date = DateTime.Today
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(transaction));

        Assert.Contains("не найдена", exception.Message);
    }

    [Fact]
    public async Task AddAsync_RejectsCategoryTypeMismatch()
    {
        using var database = new SqliteTestDatabase();
        var salary = database.SeedCategory("Зарплата", TransactionType.Income);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = salary.Id,
            Date = DateTime.Today
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(transaction));

        Assert.Contains("Тип категории", exception.Message);
    }

    [Fact]
    public async Task AddAsync_RejectsDefaultDate()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = default
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAsync(transaction));
    }

    [Fact]
    public async Task AddAsync_RejectsTooLongDescription()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var transaction = new Transaction
        {
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today,
            Description = new string('а', TransactionRules.MaxDescriptionLength + 1)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAsync(transaction));
    }

    // ===================== Чтение =====================

    [Fact]
    public async Task GetAllAsync_ReturnsNewestFirstThenByIdDescending()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var first = await service.AddAsync(Create(category, 10m, new DateTime(2026, 11, 1)));
        var second = await service.AddAsync(Create(category, 20m, new DateTime(2026, 11, 5)));
        var third = await service.AddAsync(Create(category, 30m, new DateTime(2026, 11, 5)));

        var result = await service.GetAllAsync();

        Assert.Equal(
            new[] { third.Id, second.Id, first.Id },
            result.Select(item => item.Id).ToArray());
        Assert.All(result, item => Assert.NotNull(item.Category));
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategoriesSortedByName()
    {
        using var database = new SqliteTestDatabase();
        database.SeedCategory("Транспорт", TransactionType.Expense);
        database.SeedCategory("Аренда", TransactionType.Expense);
        database.SeedCategory("Зарплата", TransactionType.Income);
        var service = database.CreateService();

        var categories = await service.GetCategoriesAsync();

        Assert.Equal(
            new[] { "Аренда", "Зарплата", "Транспорт" },
            categories.Select(category => category.Name).ToArray());
    }

    // ===================== Редактирование =====================

    [Fact]
    public async Task UpdateAsync_UpdatesAllFields()
    {
        using var database = new SqliteTestDatabase();
        var expenseCategory = database.SeedCategory("Продукты", TransactionType.Expense);
        var incomeCategory = database.SeedCategory("Зарплата", TransactionType.Income);
        var service = database.CreateService();

        var saved = await service.AddAsync(Create(expenseCategory, 10m, new DateTime(2026, 11, 1)));

        var update = new Transaction
        {
            Id = saved.Id,
            Amount = 500m,
            Type = TransactionType.Income,
            CategoryId = incomeCategory.Id,
            Date = new DateTime(2026, 11, 2),
            Description = "Премия"
        };

        var updated = await service.UpdateAsync(update);

        Assert.Equal(saved.Id, updated.Id);
        Assert.Equal(500m, updated.Amount);
        Assert.Equal(TransactionType.Income, updated.Type);
        Assert.Equal(incomeCategory.Id, updated.CategoryId);
        Assert.Equal(new DateTime(2026, 11, 2), updated.Date);
        Assert.Equal("Премия", updated.Description);

        using var db = database.CreateContext();
        var stored = await db.Transactions.SingleAsync();
        Assert.Equal(500m, stored.Amount);
        Assert.Equal(incomeCategory.Id, stored.CategoryId);
    }

    [Fact]
    public async Task UpdateAsync_MissingTransactionThrows()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var update = new Transaction
        {
            Id = 999,
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateAsync(update));
    }

    [Fact]
    public async Task UpdateAsync_RejectsCategoryTypeMismatch()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var saved = await service.AddAsync(Create(category, 10m, DateTime.Today));

        var update = new Transaction
        {
            Id = saved.Id,
            Amount = 10m,
            Type = TransactionType.Income,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(update));
    }

    [Fact]
    public async Task UpdateAsync_RejectsNonPositiveId()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var update = new Transaction
        {
            Id = 0,
            Amount = 10m,
            Type = TransactionType.Expense,
            CategoryId = category.Id,
            Date = DateTime.Today
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.UpdateAsync(update));
    }

    // ===================== Удаление =====================

    [Fact]
    public async Task DeleteAsync_RemovesExistingTransaction()
    {
        using var database = new SqliteTestDatabase();
        var category = database.SeedCategory("Продукты", TransactionType.Expense);
        var service = database.CreateService();

        var saved = await service.AddAsync(Create(category, 10m, DateTime.Today));

        bool deleted = await service.DeleteAsync(saved.Id);

        Assert.True(deleted);
        Assert.Empty(await service.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_UnknownIdReturnsFalse()
    {
        using var database = new SqliteTestDatabase();
        var service = database.CreateService();

        Assert.False(await service.DeleteAsync(12345));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeleteAsync_NonPositiveIdReturnsFalse(int id)
    {
        using var database = new SqliteTestDatabase();
        var service = database.CreateService();

        Assert.False(await service.DeleteAsync(id));
    }

    // ===================== Изоляция =====================

    [Fact]
    public async Task SeparateDatabases_DoNotShareData()
    {
        using var first = new SqliteTestDatabase();
        using var second = new SqliteTestDatabase();

        var firstCategory = first.SeedCategory("Продукты", TransactionType.Expense);
        var firstService = first.CreateService();
        await firstService.AddAsync(Create(firstCategory, 10m, DateTime.Today));

        var secondService = second.CreateService();

        Assert.Single(await firstService.GetAllAsync());
        Assert.Empty(await secondService.GetAllAsync());
    }

    private static Transaction Create(
        Category category,
        decimal amount,
        DateTime date,
        string? description = null)
    {
        return new Transaction
        {
            Amount = amount,
            Type = category.Type,
            CategoryId = category.Id,
            Date = date,
            Description = description
        };
    }
}
