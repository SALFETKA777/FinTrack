using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinTrack.Models;

namespace FinTrack.Services;

/// <summary>
/// Операции над транзакциями. Интерфейс позволяет подменять хранилище
/// в тестах, не обращаясь к реальной базе пользователя.
/// </summary>
public interface ITransactionService
{
    /// <summary>Возвращает все транзакции вместе с категориями.</summary>
    Task<List<Transaction>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает категории, отсортированные по названию.</summary>
    Task<List<Category>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Добавляет транзакцию и возвращает её с заполненным Id.</summary>
    Task<Transaction> AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет существующую транзакцию.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Запись с таким Id не найдена.</exception>
    Task<Transaction> UpdateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет транзакцию по Id.
    /// </summary>
    /// <returns>true — запись удалена; false — записи не было.</returns>
    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
