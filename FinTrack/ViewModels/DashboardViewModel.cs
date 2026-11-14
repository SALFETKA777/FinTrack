using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Models;
using FinTrack.Services;

namespace FinTrack.ViewModels;

/// <summary>
/// ViewModel главной панели: общий баланс, итоги выбранного месяца,
/// данные для графиков и последние операции.
/// Транзакции загружаются один раз и хранятся в памяти — смена месяца
/// пересчитывает показатели без повторных запросов к базе.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    /// <summary>Сколько месяцев показывать на графике.</summary>
    private const int ChartMonths = 6;

    /// <summary>Сколько категорий показывать отдельно на круговой диаграмме.</summary>
    private const int TopCategoriesCount = 6;

    /// <summary>Сколько последних транзакций показывать.</summary>
    private const int RecentTransactionsCount = 5;

    private readonly Func<Task<List<Transaction>>> _loadTransactions;
    private readonly StatisticsService _statisticsService;

    /// <summary>Кеш загруженных транзакций. Смена месяца его не перезапрашивает.</summary>
    private IReadOnlyList<Transaction> _cachedTransactions = Array.Empty<Transaction>();

    public DashboardViewModel(
        TransactionService transactionService,
        StatisticsService statisticsService)
        : this(CreateTransactionLoader(transactionService), statisticsService)
    {
    }

    /// <summary>
    /// Конструктор с подменяемым источником транзакций.
    /// Используется тестами, чтобы не обращаться к реальной базе данных.
    /// </summary>
    public DashboardViewModel(
        Func<Task<List<Transaction>>> loadTransactions,
        StatisticsService statisticsService)
    {
        _loadTransactions = loadTransactions
            ?? throw new ArgumentNullException(nameof(loadTransactions));

        _statisticsService = statisticsService
            ?? throw new ArgumentNullException(nameof(statisticsService));

        // Установка месяца запускает OnSelectedMonthChanged → первичный расчёт (нули).
        SelectedMonth = GetCurrentMonthStart();
    }

    private static Func<Task<List<Transaction>>> CreateTransactionLoader(
        TransactionService transactionService)
    {
        ArgumentNullException.ThrowIfNull(transactionService);

        return () => transactionService.GetAllAsync();
    }

    /// <summary>Данные для графика доходов и расходов по месяцам.</summary>
    public ObservableCollection<MonthlySummary> MonthlySummaries { get; } = new();

    /// <summary>Расходы по категориям за выбранный месяц.</summary>
    public ObservableCollection<CategoryShare> CategoryShares { get; } = new();

    /// <summary>Последние транзакции за всё время.</summary>
    public ObservableCollection<Transaction> RecentTransactions { get; } = new();

    /// <summary>Выбранный месяц (всегда первое число, 00:00).</summary>
    [ObservableProperty]
    public partial DateTime SelectedMonth { get; set; }

    /// <summary>Название выбранного месяца для интерфейса, например «Ноябрь 2026».</summary>
    [ObservableProperty]
    public partial string MonthTitle { get; set; } = string.Empty;

    /// <summary>Общий баланс за всё время.</summary>
    [ObservableProperty]
    public partial decimal TotalBalance { get; set; }

    /// <summary>Доходы за выбранный месяц.</summary>
    [ObservableProperty]
    public partial decimal MonthIncome { get; set; }

    /// <summary>Расходы за выбранный месяц.</summary>
    [ObservableProperty]
    public partial decimal MonthExpense { get; set; }

    /// <summary>Баланс выбранного месяца.</summary>
    [ObservableProperty]
    public partial decimal MonthBalance { get; set; }

    /// <summary>Признак выполнения загрузки данных.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Текст ошибки для показа пользователю. null — ошибок нет.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>true, если за выбранный месяц есть расходы по категориям.</summary>
    [ObservableProperty]
    public partial bool HasCategoryExpenses { get; set; }

    /// <summary>true, если есть последние операции для показа.</summary>
    [ObservableProperty]
    public partial bool HasRecentTransactions { get; set; }

    /// <summary>
    /// Публичное обновление данных: перечитывает транзакции из базы.
    /// Нужен, чтобы Dashboard обновился после изменений на экране транзакций.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var transactions = await _loadTransactions();

            _cachedTransactions = transactions;
            Recalculate();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось загрузить данные: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Команда для вызова из View при открытии экрана.</summary>
    [RelayCommand]
    private Task LoadAsync() => RefreshAsync();

    /// <summary>Переход к предыдущему месяцу.</summary>
    [RelayCommand]
    private void PreviousMonth()
    {
        SelectedMonth = SelectedMonth.AddMonths(-1);
    }

    /// <summary>Переход к следующему месяцу. В будущее переходить нельзя.</summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextMonth))]
    private void NextMonth()
    {
        SelectedMonth = SelectedMonth.AddMonths(1);
    }

    private bool CanGoToNextMonth()
    {
        return SelectedMonth.AddMonths(1) <= GetCurrentMonthStart();
    }

    /// <summary>
    /// Пересчитывает все показатели из кеша. Вызывается после загрузки
    /// и при смене месяца.
    /// </summary>
    private void Recalculate()
    {
        TotalBalance = _statisticsService.CalculateTotalBalance(_cachedTransactions);

        MonthIncome = _statisticsService.CalculateMonthIncome(
            _cachedTransactions, SelectedMonth);

        MonthExpense = _statisticsService.CalculateMonthExpense(
            _cachedTransactions, SelectedMonth);

        MonthBalance = MonthIncome - MonthExpense;

        ReplaceItems(
            MonthlySummaries,
            _statisticsService.BuildMonthlySummaries(
                _cachedTransactions, SelectedMonth, ChartMonths));

        ReplaceItems(
            CategoryShares,
            _statisticsService.BuildCategoryShares(
                _cachedTransactions, SelectedMonth, TopCategoriesCount));

        ReplaceItems(
            RecentTransactions,
            _statisticsService.GetRecentTransactions(
                _cachedTransactions, RecentTransactionsCount));

        HasCategoryExpenses = CategoryShares.Count > 0;
        HasRecentTransactions = RecentTransactions.Count > 0;
    }

    private static void ReplaceItems<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    partial void OnSelectedMonthChanged(DateTime value)
    {
        MonthTitle = FormatMonth(value);
        NextMonthCommand.NotifyCanExecuteChanged();
        Recalculate();
    }

    private static DateTime GetCurrentMonthStart()
    {
        var today = DateTime.Today;
        return new DateTime(today.Year, today.Month, 1);
    }

    private static string FormatMonth(DateTime month)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        string text = month.ToString("MMMM yyyy", culture);

        return text.Length == 0
            ? string.Empty
            : char.ToUpper(text[0], culture) + text[1..];
    }
}
