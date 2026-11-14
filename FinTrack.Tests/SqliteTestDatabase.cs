using System;
using System.IO;
using FinTrack.Data;
using FinTrack.Models;
using FinTrack.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Tests;

/// <summary>
/// Отдельная временная SQLite-база для одного теста.
/// Каждый экземпляр получает уникальный файл, поэтому тесты можно
/// запускать параллельно, не затрагивая базу пользователя.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _directory;
    private bool _disposed;

    public SqliteTestDatabase()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "FinTrack.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_directory);

        DatabasePath = Path.Combine(_directory, "test.db");

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Путь к временному файлу базы.</summary>
    public string DatabasePath { get; }

    /// <summary>Опции контекста, привязанные к временному файлу.</summary>
    public DbContextOptions<AppDbContext> Options { get; }

    public AppDbContext CreateContext() => new(Options);

    public TransactionService CreateService() => new(CreateContext);

    /// <summary>Добавляет категорию и возвращает её с заполненным Id.</summary>
    public Category SeedCategory(
        string name,
        TransactionType type,
        string color = "#123456")
    {
        using var db = CreateContext();

        var category = new Category
        {
            Name = name,
            Type = type,
            Color = color
        };

        db.Categories.Add(category);
        db.SaveChanges();

        return category;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // SQLite держит файл открытым через пул соединений —
        // освобождаем его, чтобы временную папку можно было удалить.
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Временная папка будет очищена ОС; тест уже завершён.
        }
        catch (UnauthorizedAccessException)
        {
            // Файл мог остаться заблокированным — не роняем тест из-за очистки.
        }
    }
}
