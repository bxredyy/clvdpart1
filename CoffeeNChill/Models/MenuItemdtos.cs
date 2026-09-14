namespace CoffeeNChill.Models;

public class MenuItemCreateDto
{
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}


public class MenuItemUpdateDto
{
    public double? Price { get; set; }
    public bool? IsAvailable { get; set; }
}


public class MenuItemResponseDto
{
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }

    public static MenuItemResponseDto FromEntity(MenuItemEntity e) => new()
    {
        Category = e.PartitionKey,
        Sku = e.RowKey,
        Name = e.Name,
        Description = e.Description,
        Price = e.Price,
        IsAvailable = e.IsAvailable
    };
}