using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinTrack.Models;
using FinTrack.Services;

namespace FinTrack.Tests;

/// <summary>
/// Сервис-заглушка для тестов ViewModel. Хранит данные в памяти,
/// позволяет подменить результат и задержать загрузку.
/// </summary>
internal sealed class FakeTransactionService : ITransactionService
{
    public List<Transaction> Transactions { get; } = new();

    public List<Category> Categories { get; } = new();

    public int NextId { get; set; } = 1;

    public int GetAllCalls { get; private set; }

    public int GetCategoriesCalls { get; private set; }

    public int AddCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public Exception? GetAllException { get; set; }

    public Exception? AddException { get; set; }

    public Exception? UpdateException { get; set; }

    public Exception? DeleteException { get; set; }

    /// <summary>Если задан, GetAllAsync ждёт завершения этого источника.</summary>
    public TaskCompletionSource? GetAllGate { get; set; }

    public Transaction? LastAdded { get; private set; }

    public Transaction? LastUpdated { get; private set; }

    public List<int> DeletedIds { get; } = new();

    public async Task<List<Transaction>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        GetAllCalls++;

        if (GetAllGate is not null)
        {
            await GetAllGate.Task.WaitAsync(cancellationToken);
        }

        if (GetAllException is not null)
        {
            throw GetAllException;
        }

        return Transactions.Select(Clone).ToList();
    }

    public Task<List<Category>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        GetCategoriesCalls++;

        return Task.FromResult(Categories
            .Select(Clone)
            .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList());
    }

    public Task<Transaction> AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        AddCalls++;

        if (AddException is not null)
        {
            throw AddException;
        }

        var stored = Clone(transaction);

        if (stored.Id == 0)
        {
            stored.Id = NextId++;
        }
        else
        {
            NextId = Math.Max(NextId, stored.Id + 1);
        }

        Transactions.Add(stored);
        LastAdded = Clone(stored);

        return Task.FromResult(Clone(stored));
    }

    public Task<Transaction> UpdateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls++;

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        int index = Transactions.FindIndex(item => item.Id == transaction.Id);

        if (index < 0)
        {
            throw new KeyNotFoundException(
                $"Транзакция с Id={transaction.Id} не найдена.");
        }

        var stored = Clone(transaction);
        Transactions[index] = stored;
        LastUpdated = Clone(stored);

        return Task.FromResult(Clone(stored));
    }

    public Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        DeleteCalls++;

        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        DeletedIds.Add(id);

        return Task.FromResult(Transactions.RemoveAll(item => item.Id == id) > 0);
    }

    private Transaction Clone(Transaction transaction)
    {
        return new Transaction
        {
            Id = transaction.Id,
            Amount = transaction.Amount,
            Type = transaction.Type,
            Date = transaction.Date,
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
            Category = transaction.Category
                ?? Categories.FirstOrDefault(
                    category => category.Id == transaction.CategoryId)
        };
    }

    private static Category Clone(Category category)
    {
        return new Category
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type,
            Color = category.Color
        };
    }
}
