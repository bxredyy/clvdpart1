namespace CoffeeNChill.Models;
using Azure;
using Azure.Data.Tables;


// Represents a row in the "MenuItems" Azure Table.
// PartitionKey = Category (e.g. "Hot Drinks"), RowKey = Item SKU (e.g. "COF-001")
public class MenuItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Category
    public string RowKey { get; set; } = string.Empty;       // SKU / Item ID
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
}


