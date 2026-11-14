
using FinTrack.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace FinTrack.Data;

public class AppDbContext : DbContext
{
    /// <summary>
    /// Production-конструктор: путь к базе определяется в <see cref="OnConfiguring"/>.
    /// </summary>
    public AppDbContext()
    {
    }

    /// <summary>
    /// Конструктор с явными опциями. Используется тестами и другими сценариями,
    /// где нужна отдельная база (например, временный SQLite-файл).
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "FinTrack");

            Directory.CreateDirectory(folder);

            string databasePath = Path.Combine(
                folder, "fintrack.db");

            optionsBuilder.UseSqlite(
                $"Data Source={databasePath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>()
            .Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Budget>()
            .Property(b => b.LimitAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Budget>()
            .HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
