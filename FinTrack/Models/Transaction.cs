using System;
using System.Collections.Generic;
using System.Text;


namespace FinTrack.Models;

public class Transaction
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    public string? Description { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    public int CategoryId { get; set; }

    public Category? Category { get; set; }
}
