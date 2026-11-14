using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTrack.Models;
using FinTrack.Services;

namespace FinTrack.ViewModels;

/// <summary>
/// ViewModel экрана «Транзакции»: загрузка списка, добавление, редактирование,
/// удаление с подтверждением, поиск, фильтры и сортировка.
/// Итоги (доходы, расходы, баланс) всегда считаются по всем операциям,
/// фильтры влияют только на отображаемый список.
/// </summary>
public partial class TransactionsViewModel : ViewModelBase
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Все категории независимо от типа. В <see cref="Categories"/> попадает
    /// только отфильтрованный список под выбранный тип операции.
    /// </summary>
    private readonly List<Category> _allCategories = new();

    /// <summary>Полный список транзакций. Фильтры не меняют его.</summary>
    private readonly List<Transaction> _allTransactions = new();

    /// <summary>Подавляет пересчёт фильтров во время пакетного изменения свойств.</summary>
    private bool _suppressFiltering;

    /// <summary>Id редактируемой операции. null — форма в режиме добавления.</summary>
    private int? _editingTransactionId;

    public TransactionsViewModel(ITransactionService transactionService)
    {
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));

        TypeOptions = new[]
        {
            new TransactionTypeOption(TransactionType.Income, "Доход"),
            new TransactionTypeOption(TransactionType.Expense, "Расход")
        };

        TypeFilterOptions = new[]
        {
            new TransactionTypeFilterOption(null, "Все типы"),
            new TransactionTypeFilterOption(TransactionType.Income, "Доходы"),
            new TransactionTypeFilterOption(TransactionType.Expense, "Расходы")
        };

        SortOptions = new[]
        {
            new SortOption(SortField.Date, descending: true, "Дата: сначала новые"),
            new SortOption(SortField.Date, descending: false, "Дата: сначала старые"),
            new SortOption(SortField.Amount, descending: true, "Сумма: по убыванию"),
            new SortOption(SortField.Amount, descending: false, "Сумма: по возрастанию"),
            new SortOption(SortField.Category, descending: false, "Категория: А–Я"),
            new SortOption(SortField.Category, descending: true, "Категория: Я–А")
        };

        // Начальные значения выставляем без промежуточных пересчётов.
        _suppressFiltering = true;

        // По умолчанию выбран расход — самая частая операция.
        SelectedTypeOption = TypeOptions[1];
        SelectedTypeFilter = TypeFilterOptions[0];
        SelectedSortOption = SortOptions[0];
        SelectedDate = DateTime.Today;

        FilterCategories.Add(CategoryFilterOption.All);
        SelectedFilterCategory = CategoryFilterOption.All;

        _suppressFiltering = false;

        ApplyFilters();
    }

    /// <summary>Отфильтрованный и отсортированный список для отображения.</summary>
    public ObservableCollection<Transaction> Transactions { get; } = new();

    /// <summary>Категории, подходящие под выбранный тип операции в форме.</summary>
    public ObservableCollection<Category> Categories { get; } = new();

    /// <summary>Варианты фильтра по категории, включая «Все категории».</summary>
    public ObservableCollection<CategoryFilterOption> FilterCategories { get; } = new();

    /// <summary>Варианты выбора типа операции с русскими подписями.</summary>
    public IReadOnlyList<TransactionTypeOption> TypeOptions { get; }

    /// <summary>Варианты фильтра по типу операции.</summary>
    public IReadOnlyList<TransactionTypeFilterOption> TypeFilterOptions { get; }

    /// <summary>Варианты сортировки списка.</summary>
    public IReadOnlyList<SortOption> SortOptions { get; }

    // ===================== Поля формы =====================

    /// <summary>Сумма в виде текста — так пользователь может ввести «1250,50».</summary>
    [ObservableProperty]
    public partial string AmountText { get; set; } = string.Empty;

    /// <summary>Выбранный тип операции (доход или расход).</summary>
    [ObservableProperty]
    public partial TransactionTypeOption? SelectedTypeOption { get; set; }

    /// <summary>Выбранная категория.</summary>
    [ObservableProperty]
    public partial Category? SelectedCategory { get; set; }

    /// <summary>Дата операции. По умолчанию — сегодня.</summary>
    [ObservableProperty]
    public partial DateTime? SelectedDate { get; set; } = DateTime.Today;

    /// <summary>Необязательное описание операции.</summary>
    [ObservableProperty]
    public partial string? Description { get; set; }

    // ===================== Режим редактирования =====================

    /// <summary>true — форма редактирует существующую операцию.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(FormSubtitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    public partial bool IsEditing { get; set; }

    /// <summary>Заголовок формы в текущем режиме.</summary>
    public string FormTitle => IsEditing
        ? "Редактирование операции"
        : "Новая транзакция";

    /// <summary>Подсказка под заголовком формы.</summary>
    public string FormSubtitle => IsEditing
        ? "Измените поля и нажмите «Сохранить»"
        : "Заполните поля и нажмите «Добавить»";

    /// <summary>Текст основной кнопки формы.</summary>
    public string SaveButtonText => IsEditing ? "Сохранить" : "Добавить";

    // ===================== Поиск, фильтры и сортировка =====================

    /// <summary>Поиск по описанию и названию категории без учёта регистра.</summary>
    [ObservableProperty]
    public partial string? SearchText { get; set; }

    /// <summary>Фильтр по типу операции.</summary>
    [ObservableProperty]
    public partial TransactionTypeFilterOption? SelectedTypeFilter { get; set; }

    /// <summary>Фильтр по категории.</summary>
    [ObservableProperty]
    public partial CategoryFilterOption? SelectedFilterCategory { get; set; }

    /// <summary>Начало периода (включительно). null — без ограничения.</summary>
    [ObservableProperty]
    public partial DateTime? FilterStartDate { get; set; }

    /// <summary>Конец периода (включительно). null — без ограничения.</summary>
    [ObservableProperty]
    public partial DateTime? FilterEndDate { get; set; }

    /// <summary>Выбранный вариант сортировки.</summary>
    [ObservableProperty]
    public partial SortOption? SelectedSortOption { get; set; }

    /// <summary>Ошибка в условиях фильтрации (например, неверный диапазон дат).</summary>
    [ObservableProperty]
    public partial string? FilterErrorMessage { get; set; }

    /// <summary>Количество операций в отфильтрованном списке.</summary>
    [ObservableProperty]
    public partial int VisibleCount { get; set; }

    /// <summary>true, если задан хотя бы один фильтр или поиск.</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedTypeFilter?.Value is not null
        || SelectedFilterCategory?.CategoryId is not null
        || FilterStartDate.HasValue
        || FilterEndDate.HasValue;

    /// <summary>Заголовок пустого состояния.</summary>
    public string EmptyTitle => HasActiveFilters
        ? "Ничего не найдено"
        : "Пока нет транзакций";

    /// <summary>Подсказка пустого состояния.</summary>
    public string EmptyDescription => HasActiveFilters
        ? "Измените поиск или сбросьте фильтры."
        : "Добавьте первый доход или расход с помощью формы выше.";

    // ===================== Состояние =====================

    /// <summary>Признак выполнения операции с базой данных.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDeleteCommand))]
    public partial bool IsLoading { get; set; }

    /// <summary>Текст ошибки для показа пользователю. null — ошибок нет.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Короткое сообщение об успешном действии.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Суммарный доход по всем транзакциям.</summary>
    [ObservableProperty]
    public partial decimal TotalIncome { get; set; }

    /// <summary>Суммарный расход по всем транзакциям.</summary>
    [ObservableProperty]
    public partial decimal TotalExpense { get; set; }

    /// <summary>Разница между доходами и расходами по всем операциям.</summary>
    [ObservableProperty]
    public partial decimal Balance { get; set; }

    /// <summary>true, если отфильтрованный список пуст.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    // ===================== Подтверждение удаления =====================

    /// <summary>Операция, ожидающая подтверждения удаления.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDeleteCommand))]
    public partial Transaction? PendingDelete { get; set; }

    /// <summary>true — пользователь видит панель подтверждения удаления.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestDeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDeleteCommand))]
    public partial bool IsDeleteConfirmationVisible { get; set; }

    /// <summary>Описание операции, которую собираются удалить.</summary>
    [ObservableProperty]
    public partial string DeleteConfirmationText { get; set; } = string.Empty;

    // ===================== Команды =====================

    /// <summary>
    /// Загружает категории и транзакции из базы данных.
    /// Вызывается при открытии экрана и по кнопке «Обновить».
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;
        FilterErrorMessage = null;

        try
        {
            // При обновлении выходим из режима редактирования, чтобы не
            // сохранить устаревшую запись.
            if (IsEditing)
            {
                ExitEditMode();
                ClearForm();
            }

            var categories = await _transactionService.GetCategoriesAsync();

            _allCategories.Clear();
            _allCategories.AddRange(categories);

            RebuildFilterCategories();
            RefreshCategories();

            await ReloadCoreAsync();
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

    /// <summary>
    /// Проверяет форму и добавляет новую либо сохраняет изменённую транзакцию.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (IsLoading)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        if (!TryBuildFormValues(out decimal amount, out string? description))
        {
            return;
        }

        var type = SelectedTypeOption!.Value;
        var category = SelectedCategory!;
        var date = SelectedDate!.Value.Date;
        bool isEditing = _editingTransactionId.HasValue;
        int? editingId = _editingTransactionId;

        IsLoading = true;

        try
        {
            if (isEditing)
            {
                var transaction = new Transaction
                {
                    Id = editingId!.Value,
                    Amount = amount,
                    Type = type,
                    CategoryId = category.Id,
                    Date = date,
                    Description = description
                };

                await _transactionService.UpdateAsync(transaction);
                await ReloadCoreAsync();

                ExitEditMode();
                ClearForm();

                StatusMessage = "Изменения сохранены.";
            }
            else
            {
                var transaction = new Transaction
                {
                    Amount = amount,
                    Type = type,
                    CategoryId = category.Id,
                    Date = date,
                    Description = description
                };

                await _transactionService.AddAsync(transaction);
                await ReloadCoreAsync();

                AmountText = string.Empty;
                Description = null;

                StatusMessage = "Транзакция добавлена.";
            }
        }
        catch (KeyNotFoundException)
        {
            // Запись могла быть удалена в другом месте — показываем актуальный список.
            await ReloadSafelyAsync();
            ExitEditMode();
            ClearForm();

            ErrorMessage = "Операция не найдена: возможно, она уже удалена. Список обновлён.";
        }
        catch (Exception ex)
        {
            ErrorMessage = isEditing
                ? $"Не удалось сохранить изменения: {ex.Message}"
                : $"Не удалось добавить транзакцию: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Загружает операцию в форму для редактирования.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit(Transaction? transaction)
    {
        if (transaction is null || IsLoading)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;

        _editingTransactionId = transaction.Id;
        IsEditing = true;

        // Смена типа синхронно перестраивает Categories и сбрасывает SelectedCategory.
        SelectedTypeOption = TypeOptions.FirstOrDefault(
            option => option.Value == transaction.Type);

        SelectedCategory = Categories.FirstOrDefault(
            category => category.Id == transaction.CategoryId);

        AmountText = transaction.Amount.ToString("0.##", CultureInfo.CurrentCulture);
        SelectedDate = transaction.Date.Date;
        Description = transaction.Description;
    }

    /// <summary>Отменяет редактирование и возвращает форму в режим добавления.</summary>
    [RelayCommand(CanExecute = nameof(CanCancelEdit))]
    private void CancelEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        ExitEditMode();
        ClearForm();

        ErrorMessage = null;
        StatusMessage = null;
    }

    /// <summary>Показывает панель подтверждения удаления.</summary>
    [RelayCommand(CanExecute = nameof(CanRequestDelete))]
    private void RequestDelete(Transaction? transaction)
    {
        if (transaction is null || IsLoading || IsDeleteConfirmationVisible)
        {
            return;
        }

        PendingDelete = transaction;
        DeleteConfirmationText = BuildDeleteConfirmationText(transaction);
        IsDeleteConfirmationVisible = true;
        ErrorMessage = null;
        StatusMessage = null;
    }

    /// <summary>Подтверждает удаление и обновляет список.</summary>
    [RelayCommand(CanExecute = nameof(CanConfirmDelete))]
    private async Task ConfirmDeleteAsync()
    {
        var target = PendingDelete;

        if (target is null || IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            bool deleted = await _transactionService.DeleteAsync(target.Id);

            if (deleted)
            {
                _allTransactions.RemoveAll(item => item.Id == target.Id);
                ApplyFilters();
                StatusMessage = "Транзакция удалена.";
            }
            else
            {
                // Запись уже удалена в другом месте — просто перечитываем список.
                await ReloadCoreAsync();
                StatusMessage = "Операция уже была удалена.";
            }

            if (_editingTransactionId == target.Id)
            {
                ExitEditMode();
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось удалить транзакцию: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            HideDeleteConfirmation();
        }
    }

    /// <summary>Закрывает панель подтверждения без удаления.</summary>
    [RelayCommand(CanExecute = nameof(CanCancelDelete))]
    private void CancelDelete()
    {
        if (IsLoading)
        {
            return;
        }

        HideDeleteConfirmation();
    }

    /// <summary>Сбрасывает поиск, фильтры и сортировку к исходному состоянию.</summary>
    [RelayCommand(CanExecute = nameof(CanResetFilters))]
    private void ResetFilters()
    {
        _suppressFiltering = true;

        SearchText = string.Empty;
        SelectedTypeFilter = TypeFilterOptions[0];
        SelectedFilterCategory = FilterCategories.Count > 0
            ? FilterCategories[0]
            : null;
        FilterStartDate = null;
        FilterEndDate = null;
        FilterErrorMessage = null;
        SelectedSortOption = SortOptions[0];

        _suppressFiltering = false;

        ApplyFilters();
    }

    // ===================== Условия доступности команд =====================

    private bool CanLoad() => !IsLoading;

    private bool CanSave() => !IsLoading;

    private bool CanEdit(Transaction? transaction) =>
        !IsLoading && !IsDeleteConfirmationVisible && transaction is not null;

    private bool CanCancelEdit() => IsEditing && !IsLoading;

    private bool CanRequestDelete(Transaction? transaction) =>
        !IsLoading && !IsDeleteConfirmationVisible && transaction is not null;

    private bool CanConfirmDelete() => !IsLoading && PendingDelete is not null;

    private bool CanCancelDelete() => IsDeleteConfirmationVisible && !IsLoading;

    private bool CanResetFilters() =>
        HasActiveFilters
        || IsSortChanged()
        || FilterErrorMessage is not null;

    // ===================== Работа со списком =====================

    /// <summary>
    /// Перечитывает транзакции из базы и обновляет отфильтрованный список.
    /// </summary>
    private async Task ReloadCoreAsync()
    {
        var transactions = await _transactionService.GetAllAsync();

        _allTransactions.Clear();
        _allTransactions.AddRange(transactions);

        ApplyFilters();
    }

    /// <summary>Перечитывает список, не скрывая исходную ошибку при сбое.</summary>
    private async Task ReloadSafelyAsync()
    {
        try
        {
            await ReloadCoreAsync();
        }
        catch
        {
            // Исходная ошибка уже будет показана пользователю.
        }
    }

    /// <summary>
    /// Применяет поиск, фильтры и сортировку к полному списку.
    /// Данные в базе не изменяются.
    /// </summary>
    private void ApplyFilters()
    {
        if (_suppressFiltering)
        {
            return;
        }

        FilterErrorMessage = null;

        bool invalidRange =
            FilterStartDate.HasValue
            && FilterEndDate.HasValue
            && FilterStartDate.Value.Date > FilterEndDate.Value.Date;

        if (invalidRange)
        {
            FilterErrorMessage =
                "Дата начала не может быть позже даты окончания.";
        }

        IEnumerable<Transaction> filtered = _allTransactions;

        string search = SearchText?.Trim() ?? string.Empty;

        if (search.Length > 0)
        {
            filtered = filtered.Where(transaction =>
                Matches(transaction.Description, search)
                || Matches(transaction.Category?.Name, search));
        }

        if (SelectedTypeFilter?.Value is TransactionType type)
        {
            filtered = filtered.Where(transaction => transaction.Type == type);
        }

        if (SelectedFilterCategory?.CategoryId is int categoryId)
        {
            filtered = filtered.Where(
                transaction => transaction.CategoryId == categoryId);
        }

        // При неверном диапазоне даты не применяются, чтобы список не
        // становился пустым из-за опечатки; пользователь видит сообщение.
        if (!invalidRange)
        {
            if (FilterStartDate.HasValue)
            {
                var start = FilterStartDate.Value.Date;
                filtered = filtered.Where(transaction => transaction.Date.Date >= start);
            }

            if (FilterEndDate.HasValue)
            {
                var end = FilterEndDate.Value.Date;
                filtered = filtered.Where(transaction => transaction.Date.Date <= end);
            }
        }

        var result = ApplySort(filtered).ToList();

        Transactions.Clear();

        foreach (var transaction in result)
        {
            Transactions.Add(transaction);
        }

        VisibleCount = Transactions.Count;
        IsEmpty = Transactions.Count == 0;

        RecalculateTotals();

        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
        ResetFiltersCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Сортировка с детерминированным дополнительным критерием Id.
    /// </summary>
    private IEnumerable<Transaction> ApplySort(IEnumerable<Transaction> source)
    {
        var option = SelectedSortOption ?? SortOptions[0];

        IOrderedEnumerable<Transaction> ordered = option.Field switch
        {
            SortField.Amount => option.Descending
                ? source.OrderByDescending(transaction => transaction.Amount)
                : source.OrderBy(transaction => transaction.Amount),

            SortField.Category => option.Descending
                ? source.OrderByDescending(
                    transaction => transaction.Category?.Name ?? string.Empty,
                    StringComparer.CurrentCultureIgnoreCase)
                : source.OrderBy(
                    transaction => transaction.Category?.Name ?? string.Empty,
                    StringComparer.CurrentCultureIgnoreCase),

            _ => option.Descending
                ? source.OrderByDescending(transaction => transaction.Date)
                : source.OrderBy(transaction => transaction.Date)
        };

        return ordered.ThenBy(transaction => transaction.Id);
    }

    /// <summary>
    /// Пересчитывает доходы, расходы и баланс по всем операциям.
    /// Считаем в памяти: SQLite-провайдер EF Core не умеет Sum по decimal в SQL.
    /// </summary>
    private void RecalculateTotals()
    {
        TotalIncome = _allTransactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);

        TotalExpense = _allTransactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        Balance = TotalIncome - TotalExpense;
    }

    /// <summary>
    /// Обновляет список категорий формы под выбранный тип операции.
    /// </summary>
    private void RefreshCategories()
    {
        Categories.Clear();
        SelectedCategory = null;

        if (SelectedTypeOption is null)
        {
            return;
        }

        foreach (var category in _allCategories.Where(
                     category => category.Type == SelectedTypeOption.Value))
        {
            Categories.Add(category);
        }
    }

    /// <summary>
    /// Перестраивает фильтр категорий, сохраняя выбранную категорию по Id.
    /// </summary>
    private void RebuildFilterCategories()
    {
        int? selectedId = SelectedFilterCategory?.CategoryId;

        _suppressFiltering = true;

        FilterCategories.Clear();
        FilterCategories.Add(CategoryFilterOption.All);

        foreach (var category in _allCategories.OrderBy(
                     category => category.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            FilterCategories.Add(new CategoryFilterOption(
                category.Id,
                category.Name,
                category.Color));
        }

        SelectedFilterCategory = selectedId.HasValue
            ? FilterCategories.FirstOrDefault(
                option => option.CategoryId == selectedId.Value)
                ?? CategoryFilterOption.All
            : CategoryFilterOption.All;

        _suppressFiltering = false;
    }

    // ===================== Валидация формы =====================

    /// <summary>
    /// Проверяет все поля формы и возвращает подготовленные значения.
    /// </summary>
    private bool TryBuildFormValues(out decimal amount, out string? description)
    {
        description = null;

        if (!TryParseAmount(out amount))
        {
            return false;
        }

        if (SelectedTypeOption is null)
        {
            ErrorMessage = "Выберите тип операции.";
            return false;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = "Выберите категорию.";
            return false;
        }

        if (SelectedCategory.Type != SelectedTypeOption.Value)
        {
            ErrorMessage = "Тип категории не совпадает с типом операции.";
            return false;
        }

        if (SelectedDate is null)
        {
            ErrorMessage = "Выберите дату операции.";
            return false;
        }

        string? text = string.IsNullOrWhiteSpace(Description)
            ? null
            : Description.Trim();

        if (text is { Length: > TransactionRules.MaxDescriptionLength })
        {
            ErrorMessage =
                $"Описание не должно быть длиннее {TransactionRules.MaxDescriptionLength} символов.";
            return false;
        }

        description = text;
        return true;
    }

    /// <summary>
    /// Разбирает введённую сумму. Поддерживает запятую и точку как разделитель.
    /// </summary>
    private bool TryParseAmount(out decimal amount)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(AmountText))
        {
            ErrorMessage = "Введите сумму.";
            return false;
        }

        string normalized = AmountText
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace(',', '.');

        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount))
        {
            ErrorMessage = "Сумма должна быть числом, например 1250,50.";
            return false;
        }

        if (amount <= 0m)
        {
            ErrorMessage = "Сумма должна быть больше нуля.";
            return false;
        }

        if (amount > TransactionRules.MaxAmount)
        {
            ErrorMessage = "Сумма превышает допустимый диапазон.";
            return false;
        }

        if (decimal.Round(amount, 2) != amount)
        {
            ErrorMessage = "Сумма должна содержать не более двух знаков после запятой.";
            return false;
        }

        return true;
    }

    // ===================== Вспомогательные методы =====================

    private static bool Matches(string? value, string search)
    {
        return value is not null
            && value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDeleteConfirmationText(Transaction transaction)
    {
        string category = transaction.Category?.Name ?? "Без категории";
        string amount = transaction.Amount.ToString("N2", CultureInfo.CurrentCulture);
        string date = transaction.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        string description = string.IsNullOrWhiteSpace(transaction.Description)
            ? string.Empty
            : $" • {transaction.Description.Trim()}";

        return $"{category}{description} • {date} • {amount} ₽";
    }

    private void ExitEditMode()
    {
        _editingTransactionId = null;
        IsEditing = false;
    }

    private void ClearForm()
    {
        AmountText = string.Empty;
        Description = null;
        SelectedDate = DateTime.Today;
    }

    private void HideDeleteConfirmation()
    {
        PendingDelete = null;
        IsDeleteConfirmationVisible = false;
    }

    private bool IsSortChanged()
    {
        return !ReferenceEquals(SelectedSortOption, SortOptions[0]);
    }

    // ===================== Реакция на изменения =====================

    partial void OnSelectedTypeOptionChanged(TransactionTypeOption? value)
    {
        // При смене типа категории должны соответствовать ему
        // (нельзя выбрать «Зарплату» для расхода).
        RefreshCategories();
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilters();

    partial void OnSelectedTypeFilterChanged(TransactionTypeFilterOption? value) =>
        ApplyFilters();

    partial void OnSelectedFilterCategoryChanged(CategoryFilterOption? value) =>
        ApplyFilters();

    partial void OnFilterStartDateChanged(DateTime? value) => ApplyFilters();

    partial void OnFilterEndDateChanged(DateTime? value) => ApplyFilters();

    partial void OnSelectedSortOptionChanged(SortOption? value) => ApplyFilters();
}

/// <summary>
/// Элемент выпадающего списка типов операций: значение плюс подпись для UI.
/// </summary>
public sealed class TransactionTypeOption
{
    public TransactionTypeOption(TransactionType value, string title)
    {
        Value = value;
        Title = title;
    }

    public TransactionType Value { get; }

    public string Title { get; }

    public override string ToString() => Title;
}

/// <summary>
/// Элемент фильтра по типу операции. null означает «Все типы».
/// </summary>
public sealed class TransactionTypeFilterOption
{
    public TransactionTypeFilterOption(TransactionType? value, string title)
    {
        Value = value;
        Title = title;
    }

    public TransactionType? Value { get; }

    public string Title { get; }

    public override string ToString() => Title;
}

/// <summary>
/// Элемент фильтра по категории. null означает «Все категории».
/// </summary>
public sealed class CategoryFilterOption
{
    public CategoryFilterOption(int? categoryId, string title, string? color = null)
    {
        CategoryId = categoryId;
        Title = title;
        Color = color;
    }

    /// <summary>Общий вариант «Все категории».</summary>
    public static CategoryFilterOption All { get; } =
        new(null, "Все категории");

    public int? CategoryId { get; }

    public string Title { get; }

    /// <summary>Цвет категории для метки, у «Все категории» — null.</summary>
    public string? Color { get; }

    public override string ToString() => Title;
}

/// <summary>Поле, по которому сортируется список.</summary>
public enum SortField
{
    Date,
    Amount,
    Category
}

/// <summary>Вариант сортировки: поле, направление и подпись.</summary>
public sealed class SortOption
{
    public SortOption(SortField field, bool descending, string title)
    {
        Field = field;
        Descending = descending;
        Title = title;
    }

    public SortField Field { get; }

    public bool Descending { get; }

    public string Title { get; }

    public override string ToString() => Title;
}
