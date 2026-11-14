using FinTrack.Models;
using FinTrack.Services;
using Xunit;

namespace FinTrack.Tests;

public class StatisticsServiceTests
{
    private readonly StatisticsService _service = new();

    // ===================== CalculateTotalBalance =====================

    [Fact]
    public void CalculateTotalBalance_OnlyIncome_ReturnsIncomeSum()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, new DateTime(2026, 1, 5), id: 1),
            TestData.CreateIncome(250.50m, new DateTime(2026, 2, 10), id: 2)
        };

        Assert.Equal(1250.50m, _service.CalculateTotalBalance(transactions));
    }

    [Fact]
    public void CalculateTotalBalance_OnlyExpenses_ReturnsNegativeSum()
    {
        var transactions = new[]
        {
            TestData.CreateExpense(300m, new DateTime(2026, 3, 1), id: 1),
            TestData.CreateExpense(45.25m, new DateTime(2026, 3, 2), id: 2)
        };

        Assert.Equal(-345.25m, _service.CalculateTotalBalance(transactions));
    }

    [Fact]
    public void CalculateTotalBalance_MixedTransactions_ReturnsDifference()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(5000m, new DateTime(2026, 4, 1), id: 1),
            TestData.CreateExpense(1200m, new DateTime(2026, 4, 2), id: 2),
            TestData.CreateIncome(800m, new DateTime(2026, 4, 3), id: 3),
            TestData.CreateExpense(100m, new DateTime(2026, 4, 4), id: 4)
        };

        Assert.Equal(4500m, _service.CalculateTotalBalance(transactions));
    }

    [Fact]
    public void CalculateTotalBalance_EmptyCollection_ReturnsZero()
    {
        Assert.Equal(0m, _service.CalculateTotalBalance(Array.Empty<Transaction>()));
    }

    // ===================== CalculateMonthIncome =====================

    [Fact]
    public void CalculateMonthIncome_CountsOnlySelectedMonth()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, new DateTime(2026, 11, 5)),
            TestData.CreateIncome(500m, new DateTime(2026, 10, 31)),
            TestData.CreateIncome(700m, new DateTime(2026, 12, 1)),
            TestData.CreateExpense(300m, new DateTime(2026, 11, 2))
        };

        Assert.Equal(1000m, _service.CalculateMonthIncome(transactions, month));
    }

    [Fact]
    public void CalculateMonthIncome_IncludesAllDaysAndTimesOfMonth()
    {
        var month = new DateTime(2026, 11, 15);
        var transactions = new[]
        {
            TestData.CreateIncome(100m, new DateTime(2026, 11, 1, 0, 0, 0)),
            TestData.CreateIncome(200m, new DateTime(2026, 11, 30, 23, 59, 59)),
            TestData.CreateIncome(50m, new DateTime(2026, 11, 15, 12, 30, 0))
        };

        Assert.Equal(350m, _service.CalculateMonthIncome(transactions, month));
    }

    [Fact]
    public void CalculateMonthIncome_IgnoresSameMonthOfAnotherYear()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateIncome(10m, new DateTime(2025, 11, 15)),
            TestData.CreateIncome(20m, new DateTime(2026, 11, 15))
        };

        Assert.Equal(20m, _service.CalculateMonthIncome(transactions, month));
    }

    [Fact]
    public void CalculateMonthIncome_EmptyMonth_ReturnsZero()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, new DateTime(2026, 10, 5))
        };

        Assert.Equal(0m, _service.CalculateMonthIncome(transactions, new DateTime(2026, 11, 1)));
    }

    // ===================== CalculateMonthExpense =====================

    [Fact]
    public void CalculateMonthExpense_CountsOnlySelectedMonth()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateExpense(250m, new DateTime(2026, 11, 7)),
            TestData.CreateExpense(999m, new DateTime(2026, 10, 20)),
            TestData.CreateExpense(500m, new DateTime(2026, 12, 2)),
            TestData.CreateIncome(1000m, new DateTime(2026, 11, 1))
        };

        Assert.Equal(250m, _service.CalculateMonthExpense(transactions, month));
    }

    [Fact]
    public void CalculateMonthExpense_IncludesAllDaysAndTimesOfMonth()
    {
        var month = new DateTime(2026, 6, 10);
        var transactions = new[]
        {
            TestData.CreateExpense(10m, new DateTime(2026, 6, 1, 0, 0, 0)),
            TestData.CreateExpense(20m, new DateTime(2026, 6, 30, 23, 59, 59)),
            TestData.CreateExpense(30m, new DateTime(2026, 6, 15, 8, 0, 0))
        };

        Assert.Equal(60m, _service.CalculateMonthExpense(transactions, month));
    }

    [Fact]
    public void CalculateMonthExpense_IgnoresSameMonthOfAnotherYear()
    {
        var month = new DateTime(2026, 6, 1);
        var transactions = new[]
        {
            TestData.CreateExpense(11m, new DateTime(2025, 6, 15)),
            TestData.CreateExpense(22m, new DateTime(2026, 6, 15))
        };

        Assert.Equal(22m, _service.CalculateMonthExpense(transactions, month));
    }

    [Fact]
    public void CalculateMonthExpense_EmptyMonth_ReturnsZero()
    {
        var transactions = new[]
        {
            TestData.CreateExpense(500m, new DateTime(2026, 5, 5))
        };

        Assert.Equal(0m, _service.CalculateMonthExpense(transactions, new DateTime(2026, 6, 1)));
    }

    // ===================== BuildMonthlySummaries =====================

    [Fact]
    public void BuildMonthlySummaries_ReturnsRequestedNumberOfMonths()
    {
        var result = _service.BuildMonthlySummaries(
            Array.Empty<Transaction>(),
            new DateTime(2026, 11, 1),
            6);

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void BuildMonthlySummaries_OrderedOldestToNewest_EndingWithEndMonth()
    {
        var result = _service.BuildMonthlySummaries(
            Array.Empty<Transaction>(),
            new DateTime(2026, 11, 20),
            6);

        Assert.Equal(new DateTime(2026, 6, 1), result[0].Month);
        Assert.Equal(new DateTime(2026, 7, 1), result[1].Month);
        Assert.Equal(new DateTime(2026, 8, 1), result[2].Month);
        Assert.Equal(new DateTime(2026, 9, 1), result[3].Month);
        Assert.Equal(new DateTime(2026, 10, 1), result[4].Month);
        Assert.Equal(new DateTime(2026, 11, 1), result[5].Month);
    }

    [Fact]
    public void BuildMonthlySummaries_GroupsIncomeAndExpensePerMonth()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, new DateTime(2026, 11, 5)),
            TestData.CreateIncome(500m, new DateTime(2026, 11, 25)),
            TestData.CreateExpense(300m, new DateTime(2026, 11, 10)),
            TestData.CreateExpense(200m, new DateTime(2026, 9, 10)),
            TestData.CreateIncome(700m, new DateTime(2026, 9, 15))
        };

        var result = _service.BuildMonthlySummaries(
            transactions,
            new DateTime(2026, 11, 1),
            6);

        var september = result.Single(item => item.Month == new DateTime(2026, 9, 1));
        Assert.Equal(700m, september.Income);
        Assert.Equal(200m, september.Expense);
        Assert.Equal(500m, september.Balance);

        var november = result.Single(item => item.Month == new DateTime(2026, 11, 1));
        Assert.Equal(1500m, november.Income);
        Assert.Equal(300m, november.Expense);
        Assert.Equal(1200m, november.Balance);
    }

    [Fact]
    public void BuildMonthlySummaries_MonthsWithoutTransactionsHaveZeros()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, new DateTime(2026, 11, 5))
        };

        var result = _service.BuildMonthlySummaries(
            transactions,
            new DateTime(2026, 11, 1),
            6);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(0m, result[i].Income);
            Assert.Equal(0m, result[i].Expense);
            Assert.Equal(0m, result[i].Balance);
        }

        Assert.Equal(1000m, result[5].Income);
    }

    [Fact]
    public void BuildMonthlySummaries_HandlesYearBoundary()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(100m, new DateTime(2025, 12, 31, 23, 59, 0)),
            TestData.CreateExpense(50m, new DateTime(2026, 1, 1, 0, 1, 0))
        };

        var result = _service.BuildMonthlySummaries(
            transactions,
            new DateTime(2026, 1, 15),
            6);

        Assert.Equal(6, result.Count);
        Assert.Equal(new DateTime(2025, 8, 1), result[0].Month);
        Assert.Equal(new DateTime(2026, 1, 1), result[5].Month);

        var december = result.Single(item => item.Month == new DateTime(2025, 12, 1));
        Assert.Equal(100m, december.Income);
        Assert.Equal(0m, december.Expense);

        var january = result.Single(item => item.Month == new DateTime(2026, 1, 1));
        Assert.Equal(0m, january.Income);
        Assert.Equal(50m, january.Expense);
    }

    [Fact]
    public void BuildMonthlySummaries_EmptyCollection_ReturnsZeroMonths()
    {
        var result = _service.BuildMonthlySummaries(
            Array.Empty<Transaction>(),
            new DateTime(2026, 11, 1),
            6);

        Assert.Equal(6, result.Count);
        Assert.All(result, item =>
        {
            Assert.Equal(0m, item.Income);
            Assert.Equal(0m, item.Expense);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildMonthlySummaries_NonPositiveCount_ReturnsEmpty(int monthsCount)
    {
        var result = _service.BuildMonthlySummaries(
            Array.Empty<Transaction>(),
            new DateTime(2026, 11, 1),
            monthsCount);

        Assert.Empty(result);
    }

    // ===================== BuildCategoryShares =====================

    [Fact]
    public void BuildCategoryShares_GroupsExpensesByCategory()
    {
        var month = new DateTime(2026, 11, 1);
        var groceries = TestData.CreateCategory(1, "Продукты", "#F59E0B");
        var transport = TestData.CreateCategory(2, "Транспорт", "#3B82F6");

        var transactions = new[]
        {
            TestData.CreateExpense(100m, new DateTime(2026, 11, 2), groceries),
            TestData.CreateExpense(150m, new DateTime(2026, 11, 15), groceries),
            TestData.CreateExpense(300m, new DateTime(2026, 11, 20), transport)
        };

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Equal(2, result.Count);
        Assert.Equal("Транспорт", result[0].CategoryName);
        Assert.Equal(300m, result[0].Amount);
        Assert.Equal("Продукты", result[1].CategoryName);
        Assert.Equal(250m, result[1].Amount);
    }

    [Fact]
    public void BuildCategoryShares_SortsByAmountDescending()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateExpense(100m, month.AddDays(1), TestData.CreateCategory(1, "Малая")),
            TestData.CreateExpense(900m, month.AddDays(2), TestData.CreateCategory(2, "Большая")),
            TestData.CreateExpense(500m, month.AddDays(3), TestData.CreateCategory(3, "Средняя"))
        };

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Equal(new[] { "Большая", "Средняя", "Малая" },
            result.Select(item => item.CategoryName).ToArray());
    }

    [Fact]
    public void BuildCategoryShares_LimitsToTopSixAndCombinesRestIntoOther()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = Enumerable.Range(1, 8)
            .Select(i => TestData.CreateExpense(
                i * 100m,
                month.AddDays(1),
                TestData.CreateCategory(i, $"Категория {i}")))
            .ToList();

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Equal(7, result.Count);
        Assert.Equal(800m, result[0].Amount);
        Assert.Equal(300m, result[5].Amount);
        Assert.Equal("Другое", result[6].CategoryName);
        Assert.Equal(300m, result[6].Amount);
        Assert.Equal(300m / 3600m, result[6].Share);
    }

    [Fact]
    public void BuildCategoryShares_OtherContainsSumOfRemainingCategories()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = Enumerable.Range(1, 7)
            .Select(i => TestData.CreateExpense(
                i * 10m,
                month.AddDays(1),
                TestData.CreateCategory(i, $"Категория {i}")))
            .ToList();

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Equal(7, result.Count);
        Assert.Equal("Другое", result[6].CategoryName);
        Assert.Equal(10m, result[6].Amount);
    }

    [Fact]
    public void BuildCategoryShares_MissingCategory_UsesFallbackNameAndColor()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            new Transaction
            {
                Id = 1,
                Amount = 100m,
                Type = TransactionType.Expense,
                Date = month.AddDays(3),
                CategoryId = 42,
                Category = null
            }
        };

        var result = _service.BuildCategoryShares(transactions, month);

        var share = Assert.Single(result);
        Assert.Equal("Без категории", share.CategoryName);
        Assert.Equal("#808080", share.Color);
        Assert.Equal(100m, share.Amount);
        Assert.Equal(1m, share.Share);
    }

    [Fact]
    public void BuildCategoryShares_SharesAreProportionalToTotal()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateExpense(100m, month.AddDays(1), TestData.CreateCategory(1, "A")),
            TestData.CreateExpense(300m, month.AddDays(2), TestData.CreateCategory(2, "B")),
            TestData.CreateExpense(200m, month.AddDays(3), TestData.CreateCategory(3, "C"))
        };

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Equal(300m / 600m, result[0].Share);
        Assert.Equal(200m / 600m, result[1].Share);
        Assert.Equal(100m / 600m, result[2].Share);
        Assert.Equal(1m, result.Sum(item => item.Share), precision: 10);
    }

    [Fact]
    public void BuildCategoryShares_IgnoresIncomeTransactions()
    {
        var month = new DateTime(2026, 11, 1);
        var category = TestData.CreateCategory(1, "Категория");
        var transactions = new[]
        {
            TestData.CreateIncome(5000m, month.AddDays(1), category),
            TestData.CreateExpense(100m, month.AddDays(2), category)
        };

        var result = _service.BuildCategoryShares(transactions, month);

        var share = Assert.Single(result);
        Assert.Equal(100m, share.Amount);
        Assert.Equal(1m, share.Share);
    }

    [Fact]
    public void BuildCategoryShares_IgnoresExpensesOfOtherMonths()
    {
        var month = new DateTime(2026, 11, 1);
        var category = TestData.CreateCategory(1, "Категория");
        var transactions = new[]
        {
            TestData.CreateExpense(100m, new DateTime(2026, 10, 31), category),
            TestData.CreateExpense(200m, new DateTime(2026, 12, 1), category)
        };

        var result = _service.BuildCategoryShares(transactions, month);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildCategoryShares_NoExpenses_ReturnsEmpty()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateIncome(1000m, month.AddDays(1), TestData.CreateCategory(1, "Зарплата"))
        };

        Assert.Empty(_service.BuildCategoryShares(transactions, month));
    }

    [Fact]
    public void BuildCategoryShares_ZeroTotal_ReturnsEmptyWithoutDivisionByZero()
    {
        var month = new DateTime(2026, 11, 1);
        var transactions = new[]
        {
            TestData.CreateExpense(0m, month.AddDays(1), TestData.CreateCategory(1, "Нулевая"))
        };

        Assert.Empty(_service.BuildCategoryShares(transactions, month));
    }

    [Fact]
    public void BuildCategoryShares_EmptyCollection_ReturnsEmpty()
    {
        Assert.Empty(_service.BuildCategoryShares(
            Array.Empty<Transaction>(),
            new DateTime(2026, 11, 1)));
    }

    // ===================== GetRecentTransactions =====================

    [Fact]
    public void GetRecentTransactions_OrdersByDateDescending()
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1m, new DateTime(2026, 1, 10), id: 1),
            TestData.CreateIncome(2m, new DateTime(2026, 3, 10), id: 2),
            TestData.CreateIncome(3m, new DateTime(2026, 2, 10), id: 3)
        };

        var result = _service.GetRecentTransactions(transactions, 5);

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void GetRecentTransactions_SameDate_OrdersByIdDescending()
    {
        var sameDate = new DateTime(2026, 5, 5);
        var transactions = new[]
        {
            TestData.CreateIncome(1m, sameDate, id: 1),
            TestData.CreateIncome(2m, sameDate, id: 3),
            TestData.CreateIncome(3m, sameDate, id: 2)
        };

        var result = _service.GetRecentTransactions(transactions, 5);

        Assert.Equal(new[] { 3, 2, 1 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void GetRecentTransactions_LimitsResultCount()
    {
        var transactions = Enumerable.Range(1, 7)
            .Select(i => TestData.CreateIncome(i, new DateTime(2026, 1, i), id: i))
            .ToList();

        var result = _service.GetRecentTransactions(transactions, 5);

        Assert.Equal(5, result.Count);
        Assert.Equal(new[] { 7, 6, 5, 4, 3 }, result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void GetRecentTransactions_EmptyCollection_ReturnsEmpty()
    {
        Assert.Empty(_service.GetRecentTransactions(Array.Empty<Transaction>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GetRecentTransactions_NonPositiveCount_ReturnsEmpty(int count)
    {
        var transactions = new[]
        {
            TestData.CreateIncome(1m, new DateTime(2026, 1, 1), id: 1)
        };

        Assert.Empty(_service.GetRecentTransactions(transactions, count));
    }
}
