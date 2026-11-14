using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinTrack.Data;
using FinTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Services;

/// <summary>
/// Ограничения, которые применяются к транзакции до сохранения.
/// Схема БД не меняется: это правила уровня приложения.
/// </summary>
public static class TransactionRules
{
    /// <summary>
    /// Максимальная сумма соответствует точности decimal(18,2):
    /// 16 цифр до запятой и 2 после.
    /// </summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>
    /// В модели описания нет ограничения длины, поэтому используется
    /// разумный предел уровня приложения.
    /// </summary>
    public const int MaxDescriptionLength = 200;
}

/// <summary>
/// Сервис для работы с транзакциями.
/// Единственное место, где выполняется доступ к таблице Transactions через EF Core.
/// На каждую операцию создаётся отдельный AppDbContext — это безопасно для SQLite
/// и не оставляет «залипших» отслеживаемых сущностей.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly Func<AppDbContext> _contextFactory;

    /// <summary>Production-сценарий: база по стандартному пути приложения.</summary>
    public TransactionService()
        : this(() => new AppDbContext())
    {
    }

    /// <summary>
    /// Конструктор с фабрикой контекста. Используется тестами,
    /// чтобы работать с временной SQLite-базой.
    /// </summary>
    public TransactionService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory
            ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Возвращает все транзакции вместе с их категориями.
    /// Сначала самые новые (по дате, затем по Id).
    /// </summary>
    public async Task<List<Transaction>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = _contextFactory();

        return await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Возвращает список категорий, отсортированный по названию.
    /// Пока используется экраном транзакций для выбора категории.
    /// Позже можно вынести в отдельный CategoryService.
    /// </summary>
    public async Task<List<Category>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = _contextFactory();

        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Добавляет новую транзакцию в базу данных.
    /// После сохранения у переданного объекта будет заполнен Id.
    /// </summary>
    /// <exception cref="ArgumentNullException">Передана пустая ссылка.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Сумма меньше или равна нулю либо превышает допустимый диапазон.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Некорректная дата, точность суммы или длина описания.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Категория не существует или её тип не совпадает с типом операции.
    /// </exception>
    public async Task<Transaction> AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        Validate(transaction);

        await using var db = _contextFactory();

        await EnsureCategoryMatchesAsync(db, transaction, cancellationToken);

        // Category не должна вставляться заново: связь задаётся через CategoryId.
        transaction.Category = null;

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    /// <summary>
    /// Обновляет существующую транзакцию.
    /// Поля Id и Category у переданного объекта не используются для вставки.
    /// </summary>
    /// <exception cref="ArgumentNullException">Передана пустая ссылка.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Некорректный Id или сумма.</exception>
    /// <exception cref="ArgumentException">
    /// Некорректная дата, точность суммы или длина описания.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Категория не существует или её тип не совпадает с типом операции.
    /// </exception>
    /// <exception cref="KeyNotFoundException">Запись с таким Id не найдена.</exception>
    public async Task<Transaction> UpdateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transaction),
                "Id транзакции должен быть положительным.");
        }

        Validate(transaction);

        await using var db = _contextFactory();

        var existing = await db.Transactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id, cancellationToken);

        if (existing is null)
        {
            throw new KeyNotFoundException(
                $"Транзакция с Id={transaction.Id} не найдена.");
        }

        await EnsureCategoryMatchesAsync(db, transaction, cancellationToken);

        existing.Amount = transaction.Amount;
        existing.Type = transaction.Type;
        existing.CategoryId = transaction.CategoryId;
        existing.Date = transaction.Date;
        existing.Description = transaction.Description;

        await db.SaveChangesAsync(cancellationToken);

        transaction.Category = null;

        return existing;
    }

    /// <summary>
    /// Удаляет транзакцию по Id.
    /// </summary>
    /// <returns>true — запись была найдена и удалена; false — записи нет.</returns>
    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var db = _contextFactory();

        var transaction = await db.Transactions.FindAsync(
            new object[] { id },
            cancellationToken);

        if (transaction is null)
        {
            return false;
        }

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Проверяет поля транзакции, не обращаясь к базе.
    /// </summary>
    private static void Validate(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transaction),
                "Сумма транзакции должна быть больше нуля.");
        }

        if (transaction.Amount > TransactionRules.MaxAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transaction),
                "Сумма транзакции превышает допустимый диапазон.");
        }

        if (decimal.Round(transaction.Amount, 2) != transaction.Amount)
        {
            throw new ArgumentException(
                "Сумма транзакции должна иметь не более двух знаков после запятой.",
                nameof(transaction));
        }

        if (!Enum.IsDefined(typeof(TransactionType), transaction.Type))
        {
            throw new ArgumentException(
                "Неизвестный тип операции.",
                nameof(transaction));
        }

        if (transaction.Date == default)
        {
            throw new ArgumentException(
                "Дата операции не задана.",
                nameof(transaction));
        }

        transaction.Description = string.IsNullOrWhiteSpace(transaction.Description)
            ? null
            : transaction.Description.Trim();

        if (transaction.Description is { Length: > TransactionRules.MaxDescriptionLength })
        {
            throw new ArgumentException(
                $"Описание не должно быть длиннее {TransactionRules.MaxDescriptionLength} символов.",
                nameof(transaction));
        }
    }

    /// <summary>
    /// Проверяет, что категория существует и её тип совпадает с типом операции.
    /// Инвариант проверяется в сервисе, а не только в интерфейсе.
    /// </summary>
    private static async Task EnsureCategoryMatchesAsync(
        AppDbContext db,
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == transaction.CategoryId,
                cancellationToken);

        if (category is null)
        {
            throw new InvalidOperationException(
                $"Категория с Id={transaction.CategoryId} не найдена.");
        }

        if (category.Type != transaction.Type)
        {
            throw new InvalidOperationException(
                "Тип категории не совпадает с типом операции.");
        }
    }
}
