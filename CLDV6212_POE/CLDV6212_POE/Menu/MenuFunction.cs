using Azure.Data.Tables;
using CLDV6212_POE.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CLDV6212_POE.Menu
{
    public class MenuFunction
    {
        private readonly ILogger<MenuFunction> _logger;
        private const string TableName = "MenuItems";

        public MenuFunction(ILogger<MenuFunction> logger)
        {
            _logger = logger;
        }

        private TableClient GetTableClient()
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
            var tableServiceClient = new TableServiceClient(connectionString);
            var tableClient = tableServiceClient.GetTableClient(TableName);
            tableClient.CreateIfNotExists();
            return tableClient;
        }

        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Processing request to create a menu item.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var item = JsonConvert.DeserializeObject<MenuItemEntity>(requestBody);

            if (item == null || string.IsNullOrEmpty(item.PartitionKey) || string.IsNullOrEmpty(item.RowKey))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Please pass valid PartitionKey, RowKey, and menu details in the request body.");
                return badResponse;
            }

            var tableClient = GetTableClient();
            await tableClient.AddEntityAsync(item);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { message = "Menu item successfully created!", item });
            return response;
        }

        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "menu")] HttpRequestData req)
        {
            _logger.LogInformation("Retrieving all menu items.");

            var tableClient = GetTableClient();
            var items = new List<MenuItemEntity>();

            await foreach (var item in tableClient.QueryAsync<MenuItemEntity>())
            {
                items.Add(item);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(items);
            return response;
        }

        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "menu/category/{category}")] HttpRequestData req,
            string category)
        {
            _logger.LogInformation($"Retrieving menu items for category: {category}");

            var tableClient = GetTableClient();
            var items = new List<MenuItemEntity>();

            string filter = $"PartitionKey eq '{category}'";
            await foreach (var item in tableClient.QueryAsync<MenuItemEntity>(filter: filter))
            {
                items.Add(item);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(items);
            return response;
        }

        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            _logger.LogInformation($"Updating menu item with Category: {category} and ID: {id}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updatedData = JsonConvert.DeserializeObject<MenuItemEntity>(requestBody);

            var tableClient = GetTableClient();
            var existingItem = await tableClient.GetEntityIfExistsAsync<MenuItemEntity>(category, id);

            if (!existingItem.HasValue)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Menu item not found.");
                return notFoundResponse;
            }

            var item = existingItem.Value;
            if (updatedData != null)
            {
                item.Price = updatedData.Price != 0 ? updatedData.Price : item.Price;
                item.IsAvailable = updatedData.IsAvailable;
                item.Description = updatedData.Description ?? item.Description;
                item.Name = updatedData.Name ?? item.Name;
            }

            await tableClient.UpdateEntityAsync(item, item.ETag, TableUpdateMode.Replace);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Menu item successfully updated!", item });
            return response;
        }

        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            _logger.LogInformation($"Deleting menu item with Category: {category} and ID: {id}");

            var tableClient = GetTableClient();
            try
            {
                await tableClient.DeleteEntityAsync(category, id);
            }
            catch (Azure.RequestFailedException)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Menu item not found.");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Menu item successfully deleted.");
            return response;
        }
    }
}