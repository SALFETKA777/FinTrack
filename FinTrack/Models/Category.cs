using System;
using System.Collections.Generic;
using System.Text;


namespace FinTrack.Models;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; }

    public string Color { get; set; } = "#808080";

    public ICollection<Transaction> Transactions { get; set; }
        = new List<Transaction>();
}
