
using FinTrack.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace FinTrack.Data;

public static class DatabaseInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();

        // Создаём базу данных, если её ещё нет
        db.Database.EnsureCreated();

        // Добавляем стандартные категории
        if (!db.Categories.Any())
        {
            var categories = new List<Category>
            {
                new Category
                {
                    Name = "Продукты",
                    Type = TransactionType.Expense,
                    Color = "#F59E0B"
                },
                new Category
                {
                    Name = "Транспорт",
                    Type = TransactionType.Expense,
                    Color = "#3B82F6"
                },
                new Category
                {
                    Name = "Развлечения",
                    Type = TransactionType.Expense,
                    Color = "#A855F7"
                },
                new Category
                {
                    Name = "Здоровье",
                    Type = TransactionType.Expense,
                    Color = "#EF4444"
                },
                new Category
                {
                    Name = "Зарплата",
                    Type = TransactionType.Income,
                    Color = "#22C55E"
                },
                new Category
                {
                    Name = "Подработка",
                    Type = TransactionType.Income,
                    Color = "#14B8A6"
                }
            };

            db.Categories.AddRange(categories);
            db.SaveChanges();
        }
    }
}
