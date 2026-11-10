namespace CoffeeNChill.Models;
    using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;



public class MenuFunctions
{
    private const string TableName = "MenuItems";
    private readonly ILogger<MenuFunctions> _logger;
    private readonly TableClient _tableClient;

    public MenuFunctions(ILogger<MenuFunctions> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableClient = tableServiceClient.GetTableClient(TableName);
      
        _tableClient.CreateIfNotExists();
    }

   
    [Function("CreateMenuItem")]
    public async Task<IActionResult> CreateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequest req)
    {
        MenuItemCreateDto? dto;
        try
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            dto = JsonSerializer.Deserialize<MenuItemCreateDto>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Request body is not valid JSON." });
        }

       
        if (dto is null ||
            string.IsNullOrWhiteSpace(dto.Category) ||
            string.IsNullOrWhiteSpace(dto.Sku) ||
            string.IsNullOrWhiteSpace(dto.Name) ||
            dto.Price < 0)
        {
            return new BadRequestObjectResult(new
            {
                error = "Category, Sku and Name are required, and Price must not be negative."
            });
        }

        var entity = new MenuItemEntity
        {
            PartitionKey = dto.Category,
            RowKey = dto.Sku,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable
        };

        try
        {
            
            await _tableClient.AddEntityAsync(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
        {
            return new ConflictObjectResult(new
            {
                error = $"Menu item '{dto.Sku}' already exists under category '{dto.Category}'."
            });
        }

        _logger.LogInformation("Created menu item {Sku} in category {Category}", dto.Sku, dto.Category);
        return new CreatedResult($"/api/menu/{dto.Category}/{dto.Sku}", MenuItemResponseDto.FromEntity(entity));
    }

    
    [Function("GetAllMenuItems")]
    public async Task<IActionResult> GetAllMenuItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequest req)
    {
        var results = new List<MenuItemResponseDto>();

        await foreach (var entity in _tableClient.QueryAsync<MenuItemEntity>())
        {
            results.Add(MenuItemResponseDto.FromEntity(entity));
        }

        return new OkObjectResult(results);
    }

    
    [Function("GetMenuItemsByCategory")]
    public async Task<IActionResult> GetMenuItemsByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequest req,
        string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return new BadRequestObjectResult(new { error = "Category route parameter is required." });
        }

        var results = new List<MenuItemResponseDto>();

        await foreach (var entity in _tableClient.QueryAsync<MenuItemEntity>(e => e.PartitionKey == category))
        {
            results.Add(MenuItemResponseDto.FromEntity(entity));
        }

        
        return new OkObjectResult(results);
    }

    
    [Function("UpdateMenuItem")]
    public async Task<IActionResult> UpdateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequest req,
        string category, string id)
    {
        MenuItemEntity existing;
        try
        {
            var response = await _tableClient.GetEntityAsync<MenuItemEntity>(category, id);
            existing = response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return new NotFoundObjectResult(new
            {
                error = $"No menu item '{id}' found under category '{category}'."
            });
        }

        MenuItemUpdateDto? dto;
        try
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            dto = JsonSerializer.Deserialize<MenuItemUpdateDto>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Request body is not valid JSON." });
        }

        if (dto is null || (dto.Price is null && dto.IsAvailable is null))
        {
            return new BadRequestObjectResult(new
            {
                error = "Provide at least one of Price or IsAvailable to update."
            });
        }

        if (dto.Price is < 0)
        {
            return new BadRequestObjectResult(new { error = "Price must not be negative." });
        }

        if (dto.Price is not null) existing.Price = dto.Price.Value;
        if (dto.IsAvailable is not null) existing.IsAvailable = dto.IsAvailable.Value;

        
        await _tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);

        _logger.LogInformation("Updated menu item {Sku} in category {Category}", id, category);
        return new OkObjectResult(MenuItemResponseDto.FromEntity(existing));
    }

    
    [Function("DeleteMenuItem")]
    public async Task<IActionResult> DeleteMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequest req,
        string category, string id)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(category, id);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return new NotFoundObjectResult(new
            {
                error = $"No menu item '{id}' found under category '{category}'."
            });
        }

        _logger.LogInformation("Deleted menu item {Sku} in category {Category}", id, category);
        return new NoContentResult();
    }
}