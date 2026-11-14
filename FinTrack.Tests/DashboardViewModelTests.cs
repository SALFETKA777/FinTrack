using System.Globalization;
using FinTrack.Models;
using FinTrack.Services;
using FinTrack.ViewModels;
using Xunit;

namespace FinTrack.Tests;

public class DashboardViewModelTests
{
    // ===================== Начальное состояние =====================

    [Fact]
    public void Constructor_InitializesEmptyStateForCurrentMonth()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));

        var currentMonth = CurrentMonthStart();

        Assert.Equal(currentMonth, viewModel.SelectedMonth);
        Assert.Equal(0m, viewModel.TotalBalance);
        Assert.Equal(0m, viewModel.MonthIncome);
        Assert.Equal(0m, viewModel.MonthExpense);
        Assert.Equal(0m, viewModel.MonthBalance);
        Assert.Equal(6, viewModel.MonthlySummaries.Count);
        Assert.All(viewModel.MonthlySummaries, item =>
        {
            Assert.Equal(0m, item.Income);
            Assert.Equal(0m, item.Expense);
        });
        Assert.Empty(viewModel.CategoryShares);
        Assert.Empty(viewModel.RecentTransactions);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.HasCategoryExpenses);
        Assert.False(viewModel.HasRecentTransactions);
    }

    [Fact]
    public void Constructor_MonthTitle_IsCapitalizedRussianMonthWithYear()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));

        var currentMonth = CurrentMonthStart();

        Assert.Equal(ExpectedMonthTitle(currentMonth), viewModel.MonthTitle);
        Assert.Contains(currentMonth.Year.ToString(CultureInfo.InvariantCulture), viewModel.MonthTitle);
    }

    // ===================== Загрузка данных =====================

    [Fact]
    public async Task RefreshAsync_PopulatesTotalsChartsAndRecentTransactions()
    {
        var currentMonth = CurrentMonthStart();
        var previousMonth = currentMonth.AddMonths(-1);

        var groceries = TestData.CreateCategory(1, "Продукты", "#F59E0B");
        var salary = TestData.CreateCategory(2, "Зарплата", "#22C55E");

        var transactions = new List<Transaction>
        {
            TestData.CreateIncome(1000m, currentMonth.AddDays(3).AddHours(12), salary, id: 1),
            TestData.CreateExpense(250m, currentMonth.AddDays(9).AddHours(9), groceries, id: 2),
            TestData.CreateExpense(100m, previousMonth.AddDays(5), groceries, id: 3)
        };

        var viewModel = CreateViewModel(() => Task.FromResult(transactions));

        await viewModel.RefreshAsync();

        Assert.Equal(650m, viewModel.TotalBalance);
        Assert.Equal(1000m, viewModel.MonthIncome);
        Assert.Equal(250m, viewModel.MonthExpense);
        Assert.Equal(750m, viewModel.MonthBalance);

        var share = Assert.Single(viewModel.CategoryShares);
        Assert.Equal("Продукты", share.CategoryName);
        Assert.Equal(250m, share.Amount);
        Assert.Equal(1m, share.Share);

        Assert.Equal(3, viewModel.RecentTransactions.Count);
        Assert.Equal(2, viewModel.RecentTransactions[0].Id);
        Assert.True(viewModel.HasCategoryExpenses);
        Assert.True(viewModel.HasRecentTransactions);
    }

    [Fact]
    public async Task RefreshAsync_NoTransactions_LeavesFlagsFalse()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));

        await viewModel.RefreshAsync();

        Assert.Equal(0m, viewModel.TotalBalance);
        Assert.Empty(viewModel.CategoryShares);
        Assert.Empty(viewModel.RecentTransactions);
        Assert.False(viewModel.HasCategoryExpenses);
        Assert.False(viewModel.HasRecentTransactions);
        Assert.Null(viewModel.ErrorMessage);
    }

    // ===================== Переключение месяцев =====================

    [Fact]
    public void NextMonthCommand_IsDisabledOnCurrentMonth()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));

        Assert.False(viewModel.NextMonthCommand.CanExecute(null));
    }

    [Fact]
    public void NextMonthCommand_ReenabledAfterMovingToPastMonth()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));
        var currentMonth = CurrentMonthStart();

        viewModel.PreviousMonthCommand.Execute(null);

        Assert.Equal(currentMonth.AddMonths(-1), viewModel.SelectedMonth);
        Assert.True(viewModel.NextMonthCommand.CanExecute(null));

        viewModel.NextMonthCommand.Execute(null);

        Assert.Equal(currentMonth, viewModel.SelectedMonth);
        Assert.False(viewModel.NextMonthCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedMonthChange_RaisesNextMonthCanExecuteChanged()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));
        int raisedCount = 0;

        viewModel.NextMonthCommand.CanExecuteChanged += (_, _) => raisedCount++;

        viewModel.SelectedMonth = CurrentMonthStart().AddMonths(-1);

        Assert.True(raisedCount > 0);
    }

    [Fact]
    public async Task MonthSwitch_RecalculatesFromCacheWithoutReloading()
    {
        var currentMonth = CurrentMonthStart();
        var previousMonth = currentMonth.AddMonths(-1);

        var groceries = TestData.CreateCategory(1, "Продукты", "#F59E0B");
        var salary = TestData.CreateCategory(2, "Зарплата", "#22C55E");

        var transactions = new List<Transaction>
        {
            TestData.CreateIncome(1000m, currentMonth.AddDays(3), salary, id: 1),
            TestData.CreateExpense(250m, currentMonth.AddDays(9), groceries, id: 2),
            TestData.CreateExpense(100m, previousMonth.AddDays(5), groceries, id: 3)
        };

        int loadCount = 0;
        var viewModel = CreateViewModel(() =>
        {
            loadCount++;
            return Task.FromResult(transactions);
        });

        await viewModel.RefreshAsync();
        Assert.Equal(1, loadCount);

        viewModel.PreviousMonthCommand.Execute(null);

        Assert.Equal(1, loadCount);
        Assert.Equal(previousMonth, viewModel.SelectedMonth);
        Assert.Equal(0m, viewModel.MonthIncome);
        Assert.Equal(100m, viewModel.MonthExpense);
        Assert.Equal(-100m, viewModel.MonthBalance);
        Assert.Equal(650m, viewModel.TotalBalance);
        Assert.Single(viewModel.CategoryShares);
        Assert.True(viewModel.HasCategoryExpenses);

        viewModel.NextMonthCommand.Execute(null);

        Assert.Equal(1, loadCount);
        Assert.Equal(1000m, viewModel.MonthIncome);
        Assert.Equal(250m, viewModel.MonthExpense);
    }

    [Fact]
    public async Task MonthSwitch_UpdatesMonthTitle()
    {
        var viewModel = CreateViewModel(() => Task.FromResult(new List<Transaction>()));

        await viewModel.RefreshAsync();
        viewModel.PreviousMonthCommand.Execute(null);

        Assert.Equal(
            ExpectedMonthTitle(CurrentMonthStart().AddMonths(-1)),
            viewModel.MonthTitle);
    }

    // ===================== Ошибки и загрузка =====================

    [Fact]
    public async Task RefreshAsync_WhenLoaderThrows_SetsErrorMessageAndResetsLoading()
    {
        var viewModel = CreateViewModel(() =>
            Task.FromException<List<Transaction>>(new InvalidOperationException("Сбой загрузки")));

        await viewModel.RefreshAsync();

        Assert.False(viewModel.IsLoading);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Не удалось загрузить данные", viewModel.ErrorMessage!);
        Assert.Contains("Сбой загрузки", viewModel.ErrorMessage!);
    }

    [Fact]
    public async Task RefreshAsync_AfterError_ClearsErrorMessageOnSuccess()
    {
        int attempt = 0;

        var viewModel = CreateViewModel(() =>
        {
            attempt++;

            return attempt == 1
                ? Task.FromException<List<Transaction>>(new InvalidOperationException("boom"))
                : Task.FromResult(new List<Transaction>());
        });

        await viewModel.RefreshAsync();
        Assert.NotNull(viewModel.ErrorMessage);

        await viewModel.RefreshAsync();

        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshAsync_SetsIsLoadingWhileAwaitingData()
    {
        var completion = new TaskCompletionSource<List<Transaction>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var viewModel = CreateViewModel(() => completion.Task);

        var refreshTask = viewModel.RefreshAsync();
        Assert.True(viewModel.IsLoading);

        completion.SetResult(new List<Transaction>());
        await refreshTask;

        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RefreshAsync_IgnoresSecondCallWhileFirstIsRunning()
    {
        var completion = new TaskCompletionSource<List<Transaction>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int loadCount = 0;

        var viewModel = CreateViewModel(() =>
        {
            loadCount++;
            return completion.Task;
        });

        var first = viewModel.RefreshAsync();
        var second = viewModel.RefreshAsync();

        Assert.Equal(1, loadCount);

        completion.SetResult(new List<Transaction>());
        await Task.WhenAll(first, second);

        Assert.Equal(1, loadCount);
        Assert.False(viewModel.IsLoading);
    }

    // ===================== Вспомогательные методы =====================

    private static DashboardViewModel CreateViewModel(
        Func<Task<List<Transaction>>> loadTransactions)
    {
        return new DashboardViewModel(loadTransactions, new StatisticsService());
    }

    private static DateTime CurrentMonthStart()
    {
        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    private static string ExpectedMonthTitle(DateTime month)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        string text = month.ToString("MMMM yyyy", culture);

        return char.ToUpper(text[0], culture) + text[1..];
    }
}
