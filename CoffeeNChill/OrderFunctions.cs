using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Queues;
using Azure.Data.Tables;
using System.Text.Json;

namespace CoffeeNChill
{
    public class OrderFunctions
    {
        private readonly ILogger<OrderFunctions> _logger;
        private readonly TableServiceClient _tableServiceClient;
        private const string QueueName = "order-processing-queue";
        private const string TableName = "Orders";

        public OrderFunctions(ILogger<OrderFunctions> logger, TableServiceClient tableServiceClient)
        {
            _logger = logger;
            _tableServiceClient = tableServiceClient;
        }

        // 1. Queue Producer Endpoint: POST /api/orders/queue
        [Function("QueueOrder")]
        public async Task<IActionResult> QueueOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to queue an order.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var orderData = JsonSerializer.Deserialize<JsonElement>(requestBody);

            string orderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var orderPayload = new
            {
                OrderId = orderId,
                CustomerName = orderData.TryGetProperty("CustomerName", out var name) ? name.GetString() : "Unknown",
                TotalPrice = orderData.TryGetProperty("TotalPrice", out var price) ? price.GetDecimal() : 0.00m,
                OrderTimestamp = DateTime.UtcNow,
                Status = "Received"
            };

            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";

            // Send to Azure Storage Queue
            QueueClient queueClient = new QueueClient(connectionString, QueueName);
            await queueClient.CreateIfNotExistsAsync();

            string messageJson = JsonSerializer.Serialize(orderPayload);
            await queueClient.SendMessageAsync(messageJson);

            return new OkObjectResult(new { message = "Order successfully queued!", orderId = orderId });
        }

        // 2. Queue-Triggered Background Worker: ProcessOrderQueue
        [Function("ProcessOrderQueue")]
        public async Task Run(
            [QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] string myQueueItem)
        {
            _logger.LogInformation($"C# Queue trigger function processed message: {myQueueItem}");

            var order = JsonSerializer.Deserialize<JsonElement>(myQueueItem);
            string orderId = order.GetProperty("OrderId").GetString()!;
            string customerName = order.GetProperty("CustomerName").GetString()!;
            decimal totalPrice = order.GetProperty("TotalPrice").GetDecimal();

            // Prepare Table Storage Client
            var tableClient = _tableServiceClient.GetTableClient(TableName);
            await tableClient.CreateIfNotExistsAsync();

            string orderDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Define Table Entity with Order Lifecycle Status updates
            var entity = new TableEntity(orderDate, orderId)
            {
                { "CustomerName", customerName },
                { "TotalPrice", totalPrice },
                { "Status", "Received" }
            };

            await tableClient.UpsertEntityAsync(entity);
            _logger.LogInformation($"Order {orderId} saved to Table Storage with status: Received");

            // Simulate Order Status Lifecycle: Received -> Preparing -> Ready -> Collected
            entity["Status"] = "Preparing";
            await tableClient.UpsertEntityAsync(entity);

            entity["Status"] = "Ready";
            await tableClient.UpsertEntityAsync(entity);

            entity["Status"] = "Collected";
            await tableClient.UpsertEntityAsync(entity);

            _logger.LogInformation($"Order {orderId} progressed through lifecycle states up to Collected.");
        }
    }
}

