using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FinTrack.Models;
using FinTrack.ViewModels;
using Xunit;

namespace FinTrack.Tests;

/// <summary>
/// Тесты ViewModel экрана транзакций на фейковом сервисе.
/// Реальная база данных не используется.
/// </summary>
public class TransactionsViewModelTests
{
    // ===================== Начальное состояние =====================

    [Fact]
    public void Constructor_SetsAddModeAndDefaults()
    {
        var fake = new FakeTransactionService();
        var viewModel = new TransactionsViewModel(fake);

        Assert.False(viewModel.IsEditing);
        Assert.Equal("Новая транзакция", viewModel.FormTitle);
        Assert.Equal("Добавить", viewModel.SaveButtonText);
        Assert.Equal(DateTime.Today, viewModel.SelectedDate);
        Assert.Equal(TransactionType.Expense, viewModel.SelectedTypeOption!.Value);
        Assert.Null(viewModel.SelectedTypeFilter!.Value);
        Assert.Null(viewModel.SelectedFilterCategory!.CategoryId);
        Assert.Equal(SortField.Date, viewModel.SelectedSortOption!.Field);
        Assert.True(viewModel.SelectedSortOption.Descending);
        Assert.True(viewModel.IsEmpty);
        Assert.Equal(0, viewModel.VisibleCount);
        Assert.False(viewModel.HasActiveFilters);
        Assert.Equal("Пока нет транзакций", viewModel.EmptyTitle);
        Assert.Equal(0m, viewModel.TotalIncome);
        Assert.Equal(0m, viewModel.TotalExpense);
        Assert.Equal(0m, viewModel.Balance);
    }

    // ===================== Загрузка =====================

    [Fact]
    public async Task LoadAsync_PopulatesTransactionsCategoriesAndTotals()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 1000m, TransactionType.Income, Date(5), income, "Зарплата");
            AddTransaction(service, 2, 250m, TransactionType.Expense, Date(9), expense, "Продукты");
        });

        Assert.Equal(2, viewModel.VisibleCount);
        Assert.Equal(2, viewModel.Transactions.Count);
        Assert.Equal(1000m, viewModel.TotalIncome);
        Assert.Equal(250m, viewModel.TotalExpense);
        Assert.Equal(750m, viewModel.Balance);
        Assert.False(viewModel.IsEmpty);
        Assert.False(viewModel.HasActiveFilters);
        Assert.Equal(3, viewModel.FilterCategories.Count);
        Assert.Single(viewModel.Categories);
        Assert.Equal("Продукты", viewModel.Categories[0].Name);
        Assert.Equal(1, fake.GetAllCalls);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_SetsIsLoadingWhileWaiting()
    {
        var fake = new FakeTransactionService
        {
            GetAllGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };

        AddCategory(fake, CreateExpenseCategory());
        var viewModel = new TransactionsViewModel(fake);

        var loadTask = viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsLoading);

        fake.GetAllGate.SetResult();
        await loadTask;

        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_IgnoresSecondCallWhileFirstIsRunning()
    {
        var fake = new FakeTransactionService
        {
            GetAllGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };

        AddCategory(fake, CreateExpenseCategory());
        var viewModel = new TransactionsViewModel(fake);

        var first = viewModel.LoadCommand.ExecuteAsync(null);
        var second = viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.GetAllCalls);

        fake.GetAllGate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, fake.GetAllCalls);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_ServiceErrorShowsMessage()
    {
        var fake = new FakeTransactionService
        {
            GetAllException = new InvalidOperationException("Сбой базы")
        };

        AddCategory(fake, CreateExpenseCategory());
        var viewModel = new TransactionsViewModel(fake);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("Не удалось загрузить данные", viewModel.ErrorMessage!);
        Assert.Contains("Сбой базы", viewModel.ErrorMessage!);
    }

    // ===================== Добавление =====================

    [Fact]
    public async Task SaveAsync_AddsTransactionAndRefreshesTotals()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, expense));

        viewModel.AmountText = "1250,50";
        viewModel.SelectedCategory = viewModel.Categories.Single();
        viewModel.SelectedDate = Date(3);
        viewModel.Description = "  Обед  ";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.AddCalls);
        Assert.NotNull(fake.LastAdded);
        Assert.Equal(1250.50m, fake.LastAdded!.Amount);
        Assert.Equal(TransactionType.Expense, fake.LastAdded.Type);
        Assert.Equal(expense.Id, fake.LastAdded.CategoryId);
        Assert.Equal(Date(3), fake.LastAdded.Date);
        Assert.Equal("Обед", fake.LastAdded.Description);

        Assert.Equal("Транзакция добавлена.", viewModel.StatusMessage);
        Assert.Equal(string.Empty, viewModel.AmountText);
        Assert.Null(viewModel.Description);
        Assert.False(viewModel.IsEditing);
        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(1250.50m, viewModel.TotalExpense);
        Assert.Equal(-1250.50m, viewModel.Balance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1,234")]
    [InlineData("10000000000000000")]
    public async Task SaveAsync_InvalidAmountIsRejected(string amountText)
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        viewModel.AmountText = amountText;
        viewModel.SelectedCategory = viewModel.Categories.Single();

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.AddCalls);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Equal(0, viewModel.VisibleCount);
    }

    [Fact]
    public async Task SaveAsync_RequiresCategory()
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        viewModel.AmountText = "10";
        viewModel.SelectedCategory = null;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.AddCalls);
        Assert.Contains("категорию", viewModel.ErrorMessage!);
    }

    [Fact]
    public async Task SaveAsync_RequiresDate()
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        viewModel.AmountText = "10";
        viewModel.SelectedCategory = viewModel.Categories.Single();
        viewModel.SelectedDate = null;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.AddCalls);
        Assert.Contains("дату", viewModel.ErrorMessage!);
    }

    [Fact]
    public async Task SaveAsync_RejectsCategoryTypeMismatch()
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        viewModel.AmountText = "10";
        viewModel.SelectedCategory = new Category
        {
            Id = 99,
            Name = "Зарплата",
            Type = TransactionType.Income
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.AddCalls);
        Assert.Contains("не совпадает", viewModel.ErrorMessage!);
    }

    [Fact]
    public async Task SaveAsync_RejectsTooLongDescription()
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        viewModel.AmountText = "10";
        viewModel.SelectedCategory = viewModel.Categories.Single();
        viewModel.Description = new string('а', 201);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.AddCalls);
        Assert.Contains("200", viewModel.ErrorMessage!);
    }

    [Fact]
    public async Task SaveAsync_ServiceErrorShowsMessageAndResetsLoading()
    {
        var (viewModel, fake) = await CreateLoadedAsync(service =>
            AddCategory(service, CreateExpenseCategory()));

        fake.AddException = new InvalidOperationException("Диск недоступен");

        viewModel.AmountText = "10";
        viewModel.SelectedCategory = viewModel.Categories.Single();

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.Equal(0, viewModel.VisibleCount);
        Assert.Contains("Не удалось добавить транзакцию", viewModel.ErrorMessage!);
        Assert.Contains("Диск недоступен", viewModel.ErrorMessage!);
    }

    // ===================== Редактирование =====================

    [Fact]
    public async Task EditCommand_LoadsTransactionIntoForm()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 1250.50m, TransactionType.Expense, Date(10), expense, "Хлеб");
        });

        viewModel.EditCommand.Execute(viewModel.Transactions.Single());

        Assert.True(viewModel.IsEditing);
        Assert.Equal("Редактирование операции", viewModel.FormTitle);
        Assert.Equal("Сохранить", viewModel.SaveButtonText);
        Assert.Equal(1250.50m, ParseAmount(viewModel.AmountText));
        Assert.Equal(Date(10), viewModel.SelectedDate);
        Assert.Equal("Хлеб", viewModel.Description);
        Assert.Equal(expense.Id, viewModel.SelectedCategory!.Id);

        Assert.Contains(viewModel.Categories, category => category.Id == expense.Id);
    }

    [Fact]
    public async Task EditCommand_ChangingTypeRefreshesCategories()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Income, Date(5), income, "Премия");
        });

        viewModel.EditCommand.Execute(viewModel.Transactions.Single());

        Assert.Equal(TransactionType.Income, viewModel.SelectedTypeOption!.Value);
        Assert.Contains(viewModel.Categories, category => category.Id == income.Id);

        viewModel.SelectedTypeOption = viewModel.TypeOptions[1];

        Assert.All(viewModel.Categories, category => Assert.Equal(TransactionType.Expense, category.Type));
        Assert.Null(viewModel.SelectedCategory);
    }

    [Fact]
    public async Task SaveAsync_WhenEditing_UpdatesInsteadOfAdding()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Старое");
        });

        viewModel.EditCommand.Execute(viewModel.Transactions.Single());
        viewModel.AmountText = "99,99";
        viewModel.Description = "Новое";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.UpdateCalls);
        Assert.Equal(0, fake.AddCalls);
        Assert.Equal(99.99m, fake.LastUpdated!.Amount);
        Assert.Equal("Новое", fake.LastUpdated.Description);
        Assert.False(viewModel.IsEditing);
        Assert.Equal("Изменения сохранены.", viewModel.StatusMessage);
        Assert.Equal(99.99m, viewModel.TotalExpense);
    }

    [Fact]
    public async Task SaveAsync_MissingTransactionShowsMessageAndReloads()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
        });

        viewModel.EditCommand.Execute(viewModel.Transactions.Single());

        fake.UpdateException = new KeyNotFoundException("Транзакция не найдена.");

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsEditing);
        Assert.False(viewModel.IsLoading);
        Assert.Contains("не найдена", viewModel.ErrorMessage!);
        Assert.True(fake.GetAllCalls >= 2);
    }

    [Fact]
    public async Task CancelEdit_ResetsFormAndMode()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Описание");
        });

        viewModel.EditCommand.Execute(viewModel.Transactions.Single());
        viewModel.CancelEditCommand.Execute(null);

        Assert.False(viewModel.IsEditing);
        Assert.Equal("Новая транзакция", viewModel.FormTitle);
        Assert.Equal(string.Empty, viewModel.AmountText);
        Assert.Null(viewModel.Description);
        Assert.Equal(DateTime.Today, viewModel.SelectedDate);
        Assert.Equal(0, fake.UpdateCalls);
        Assert.Equal(0, fake.AddCalls);
    }

    // ===================== Удаление с подтверждением =====================

    [Fact]
    public async Task RequestDelete_ShowsConfirmationWithoutDeleting()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Хлеб");
        });

        var target = viewModel.Transactions.Single();

        viewModel.RequestDeleteCommand.Execute(target);

        Assert.True(viewModel.IsDeleteConfirmationVisible);
        Assert.Same(target, viewModel.PendingDelete);
        Assert.Contains("Продукты", viewModel.DeleteConfirmationText);
        Assert.Contains("Хлеб", viewModel.DeleteConfirmationText);
        Assert.Contains("₽", viewModel.DeleteConfirmationText);
        Assert.Equal(0, fake.DeleteCalls);
        Assert.Single(viewModel.Transactions);
    }

    [Fact]
    public async Task ConfirmDelete_RemovesTransactionAndHidesPanel()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
        });

        var target = viewModel.Transactions.Single();

        viewModel.RequestDeleteCommand.Execute(target);
        await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.DeleteCalls);
        Assert.Contains(target.Id, fake.DeletedIds);
        Assert.Empty(viewModel.Transactions);
        Assert.True(viewModel.IsEmpty);
        Assert.Equal(0m, viewModel.TotalExpense);
        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Null(viewModel.PendingDelete);
        Assert.Equal("Транзакция удалена.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task CancelDelete_KeepsTransaction()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
        });

        viewModel.RequestDeleteCommand.Execute(viewModel.Transactions.Single());
        viewModel.CancelDeleteCommand.Execute(null);

        Assert.Equal(0, fake.DeleteCalls);
        Assert.Single(viewModel.Transactions);
        Assert.False(viewModel.IsDeleteConfirmationVisible);
        Assert.Null(viewModel.PendingDelete);
    }

    [Fact]
    public async Task ConfirmDelete_ServiceErrorShowsMessageAndHidesPanel()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, fake) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
        });

        fake.DeleteException = new InvalidOperationException("Ошибка удаления");

        viewModel.RequestDeleteCommand.Execute(viewModel.Transactions.Single());
        await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.Contains("Не удалось удалить транзакцию", viewModel.ErrorMessage!);
        Assert.Single(viewModel.Transactions);
        Assert.False(viewModel.IsDeleteConfirmationVisible);
    }

    [Fact]
    public async Task RequestDelete_SecondRequestBlockedWhileConfirmationIsOpen()
    {
        var expense = CreateExpenseCategory();
        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
            AddTransaction(service, 2, 200m, TransactionType.Expense, Date(2), expense);
        });

        var first = viewModel.Transactions[0];
        var second = viewModel.Transactions[1];

        viewModel.RequestDeleteCommand.Execute(first);

        Assert.False(viewModel.RequestDeleteCommand.CanExecute(second));

        viewModel.RequestDeleteCommand.Execute(second);

        Assert.Same(first, viewModel.PendingDelete);
    }

    // ===================== Поиск, фильтры и сортировка =====================

    [Fact]
    public async Task Search_FindsByDescriptionAndCategoryIgnoringCase()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Хлеб и молоко");
            AddTransaction(service, 2, 500m, TransactionType.Income, Date(2), income, "Премия");
        });

        viewModel.SearchText = "хЛЕБ";

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal("Продукты", viewModel.Transactions.Single().Category!.Name);
        Assert.True(viewModel.HasActiveFilters);
        Assert.Equal("Ничего не найдено", viewModel.EmptyTitle);

        viewModel.SearchText = "ЗАРПЛАТА";

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(TransactionType.Income, viewModel.Transactions.Single().Type);

        viewModel.SearchText = string.Empty;

        Assert.Equal(2, viewModel.VisibleCount);
        Assert.False(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task TypeFilter_ShowsOnlySelectedType()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
            AddTransaction(service, 2, 500m, TransactionType.Income, Date(2), income);
        });

        viewModel.SelectedTypeFilter = viewModel.TypeFilterOptions[1];

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(TransactionType.Income, viewModel.Transactions.Single().Type);

        viewModel.SelectedTypeFilter = viewModel.TypeFilterOptions[2];

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(TransactionType.Expense, viewModel.Transactions.Single().Type);

        viewModel.SelectedTypeFilter = viewModel.TypeFilterOptions[0];

        Assert.Equal(2, viewModel.VisibleCount);
    }

    [Fact]
    public async Task CategoryFilter_ShowsOnlySelectedCategory()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense);
            AddTransaction(service, 2, 500m, TransactionType.Income, Date(2), income);
        });

        viewModel.SelectedFilterCategory = viewModel.FilterCategories
            .Single(option => option.CategoryId == income.Id);

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(income.Id, viewModel.Transactions.Single().CategoryId);
    }

    [Fact]
    public async Task DateFilter_IncludesBothBoundaries()
    {
        var expense = CreateExpenseCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, new DateTime(2026, 10, 31), expense);
            AddTransaction(service, 2, 200m, TransactionType.Expense, new DateTime(2026, 11, 1), expense);
            AddTransaction(service, 3, 300m, TransactionType.Expense, new DateTime(2026, 11, 30), expense);
            AddTransaction(service, 4, 400m, TransactionType.Expense, new DateTime(2026, 12, 1), expense);
        });

        viewModel.FilterStartDate = new DateTime(2026, 11, 1);
        viewModel.FilterEndDate = new DateTime(2026, 11, 30);

        Assert.Equal(2, viewModel.VisibleCount);
        Assert.Equal(
            new[] { 200m, 300m },
            viewModel.Transactions.Select(item => item.Amount).OrderBy(value => value).ToArray());
        Assert.Null(viewModel.FilterErrorMessage);
    }

    [Fact]
    public async Task InvalidDateRange_ShowsMessageAndIgnoresDates()
    {
        var expense = CreateExpenseCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddTransaction(service, 1, 100m, TransactionType.Expense, new DateTime(2026, 11, 1), expense);
            AddTransaction(service, 2, 200m, TransactionType.Expense, new DateTime(2026, 11, 2), expense);
        });

        viewModel.FilterStartDate = new DateTime(2026, 11, 30);
        viewModel.FilterEndDate = new DateTime(2026, 11, 1);

        Assert.NotNull(viewModel.FilterErrorMessage);
        Assert.Contains("не может быть позже", viewModel.FilterErrorMessage!);
        Assert.Equal(2, viewModel.VisibleCount);

        viewModel.ResetFiltersCommand.Execute(null);

        Assert.Null(viewModel.FilterErrorMessage);
        Assert.Null(viewModel.FilterStartDate);
        Assert.Null(viewModel.FilterEndDate);
    }

    [Fact]
    public async Task Sorting_WorksForAmountCategoryAndDateWithIdTieBreak()
    {
        var expenseA = CreateExpenseCategory("Аренда", 1);
        var expenseB = CreateExpenseCategory("Бензин", 2);
        var expenseC = CreateExpenseCategory("Вода", 3);

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expenseA);
            AddCategory(service, expenseB);
            AddCategory(service, expenseC);
            AddTransaction(service, 1, 300m, TransactionType.Expense, Date(1), expenseB);
            AddTransaction(service, 2, 100m, TransactionType.Expense, Date(3), expenseA);
            AddTransaction(service, 3, 100m, TransactionType.Expense, Date(2), expenseC);
        });

        viewModel.SelectedSortOption = viewModel.SortOptions.Single(
            option => option.Field == SortField.Amount && !option.Descending);

        Assert.Equal(
            new[] { 100m, 100m, 300m },
            viewModel.Transactions.Select(item => item.Amount).ToArray());
        // При равных суммах порядок стабилен по Id.
        Assert.Equal(
            new[] { 2, 3, 1 },
            viewModel.Transactions.Select(item => item.Id).ToArray());

        viewModel.SelectedSortOption = viewModel.SortOptions.Single(
            option => option.Field == SortField.Amount && option.Descending);

        Assert.Equal(
            new[] { 300m, 100m, 100m },
            viewModel.Transactions.Select(item => item.Amount).ToArray());

        viewModel.SelectedSortOption = viewModel.SortOptions.Single(
            option => option.Field == SortField.Category && !option.Descending);

        Assert.Equal(
            new[] { "Аренда", "Бензин", "Вода" },
            viewModel.Transactions.Select(item => item.Category!.Name).ToArray());

        viewModel.SelectedSortOption = viewModel.SortOptions.Single(
            option => option.Field == SortField.Date && !option.Descending);

        Assert.Equal(
            new[] { 1, 3, 2 },
            viewModel.Transactions.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task ResetFilters_RestoresInitialListAndSorting()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Хлеб");
            AddTransaction(service, 2, 500m, TransactionType.Income, Date(2), income, "Премия");
        });

        viewModel.SearchText = "Хлеб";
        viewModel.SelectedTypeFilter = viewModel.TypeFilterOptions[2];
        viewModel.SelectedFilterCategory = viewModel.FilterCategories
            .Single(option => option.CategoryId == expense.Id);
        viewModel.FilterStartDate = Date(1);
        viewModel.FilterEndDate = Date(10);
        viewModel.SelectedSortOption = viewModel.SortOptions[3];

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.True(viewModel.HasActiveFilters);

        viewModel.ResetFiltersCommand.Execute(null);

        Assert.Equal(2, viewModel.VisibleCount);
        Assert.False(viewModel.HasActiveFilters);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Null(viewModel.SelectedTypeFilter!.Value);
        Assert.Null(viewModel.SelectedFilterCategory!.CategoryId);
        Assert.Null(viewModel.FilterStartDate);
        Assert.Null(viewModel.FilterEndDate);
        Assert.Equal(SortField.Date, viewModel.SelectedSortOption!.Field);
        Assert.True(viewModel.SelectedSortOption.Descending);
    }

    [Fact]
    public async Task Totals_RemainForAllTransactionsWhenListIsFiltered()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 1000m, TransactionType.Income, Date(1), income, "Зарплата");
            AddTransaction(service, 2, 200m, TransactionType.Expense, Date(2), expense, "Продукты");
            AddTransaction(service, 3, 300m, TransactionType.Expense, Date(3), expense, "Такси");
        });

        viewModel.SearchText = "Такси";

        Assert.Equal(1, viewModel.VisibleCount);
        Assert.Equal(1000m, viewModel.TotalIncome);
        Assert.Equal(500m, viewModel.TotalExpense);
        Assert.Equal(500m, viewModel.Balance);
    }

    [Fact]
    public async Task RepeatedFiltering_DoesNotDuplicateItems()
    {
        var expense = CreateExpenseCategory();
        var income = CreateIncomeCategory();

        var (viewModel, _) = await CreateLoadedAsync(service =>
        {
            AddCategory(service, expense);
            AddCategory(service, income);
            AddTransaction(service, 1, 100m, TransactionType.Expense, Date(1), expense, "Хлеб");
            AddTransaction(service, 2, 500m, TransactionType.Income, Date(2), income, "Премия");
        });

        viewModel.SearchText = "е";
        viewModel.SelectedTypeFilter = viewModel.TypeFilterOptions[0];
        viewModel.SearchText = "е";
        viewModel.SelectedSortOption = viewModel.SortOptions[1];

        Assert.Equal(
            viewModel.Transactions.Count,
            viewModel.Transactions.Select(item => item.Id).Distinct().Count());
        Assert.Equal(2, viewModel.Transactions.Count);
    }

    // ===================== Блокировки во время загрузки =====================

    [Fact]
    public async Task Commands_AreDisabledWhileLoading()
    {
        var fake = new FakeTransactionService
        {
            GetAllGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };

        var expense = CreateExpenseCategory();
        AddCategory(fake, expense);
        var transaction = AddTransaction(
            fake, 1, 100m, TransactionType.Expense, Date(1), expense);

        var viewModel = new TransactionsViewModel(fake);

        var loadTask = viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.EditCommand.CanExecute(transaction));
        Assert.False(viewModel.RequestDeleteCommand.CanExecute(transaction));
        Assert.False(viewModel.ConfirmDeleteCommand.CanExecute(null));

        fake.GetAllGate.SetResult();
        await loadTask;

        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.EditCommand.CanExecute(transaction));
    }

    // ===================== Вспомогательные методы =====================

    private static async Task<(TransactionsViewModel ViewModel, FakeTransactionService Fake)>
        CreateLoadedAsync(Action<FakeTransactionService>? seed = null)
    {
        var fake = new FakeTransactionService();

        seed?.Invoke(fake);

        var viewModel = new TransactionsViewModel(fake);
        await viewModel.LoadCommand.ExecuteAsync(null);

        return (viewModel, fake);
    }

    private static Category CreateExpenseCategory(
        string name = "Продукты",
        int id = 1)
    {
        return new Category
        {
            Id = id,
            Name = name,
            Type = TransactionType.Expense,
            Color = "#F59E0B"
        };
    }

    private static Category CreateIncomeCategory()
    {
        return new Category
        {
            Id = 2,
            Name = "Зарплата",
            Type = TransactionType.Income,
            Color = "#22C55E"
        };
    }

    private static Category AddCategory(
        FakeTransactionService fake,
        Category category)
    {
        fake.Categories.Add(category);
        return category;
    }

    private static Transaction AddTransaction(
        FakeTransactionService fake,
        int id,
        decimal amount,
        TransactionType type,
        DateTime date,
        Category category,
        string? description = null)
    {
        var transaction = new Transaction
        {
            Id = id,
            Amount = amount,
            Type = type,
            Date = date,
            CategoryId = category.Id,
            Category = category,
            Description = description
        };

        fake.Transactions.Add(transaction);
        fake.NextId = Math.Max(fake.NextId, id + 1);

        return transaction;
    }

    private static DateTime Date(int day) => new(2026, 11, day);

    private static decimal ParseAmount(string text)
    {
        return decimal.Parse(
            text.Replace(" ", string.Empty).Replace(',', '.'),
            CultureInfo.InvariantCulture);
    }
}
