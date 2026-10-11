using Azure.Data.Tables;
using Azure.Storage.Queues;
using CLDV6212_POE.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoffeeNChill
{
  
    public class OrderFunctions
    {
        private const string QueueName = "order-processing-queue";
        private const string PoisonQueueName = "order-processing-queue-poison"; // created automatically by the Functions host
        private const string TableName = "Orders";

        // Delay between lifecycle transitions so each status can be observed in the Orders table (demo/simulation)
        private static readonly TimeSpan StatusDelay = TimeSpan.FromSeconds(3);

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly ILogger<OrderFunctions> _logger;
        private readonly TableServiceClient _tableServiceClient;
        private readonly QueueServiceClient _queueServiceClient;

        public OrderFunctions(
            ILogger<OrderFunctions> logger,
            TableServiceClient tableServiceClient,
            QueueServiceClient queueServiceClient)
        {
            _logger = logger;
            _tableServiceClient = tableServiceClient;
            _queueServiceClient = queueServiceClient;
        }

        [Function("QueueOrder")]
        public async Task<IActionResult> QueueOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequest req)
        {
            _logger.LogInformation("QueueOrder: received request to queue an order.");

            // --- Parse body (400 on malformed JSON) ---
            OrderRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "QueueOrder: malformed JSON body.");
                return new BadRequestObjectResult(new { error = "Request body is not valid JSON." });
            }

            if (request is null)
                return new BadRequestObjectResult(new { error = "Request body is required." });

            // --- Validate fields (400 with a list of every problem found) ---
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.CustomerName))
                errors.Add("CustomerName is required.");

            if (request.SelectedItemSKUs is null || request.SelectedItemSKUs.Count == 0)
                errors.Add("SelectedItemSKUs must contain at least one SKU.");
            else if (request.SelectedItemSKUs.Any(string.IsNullOrWhiteSpace))
                errors.Add("SelectedItemSKUs cannot contain empty values.");

            if (request.TotalPrice is null || request.TotalPrice <= 0)
                errors.Add("TotalPrice must be greater than 0.");

            if (errors.Count > 0)
                return new BadRequestObjectResult(new { error = "Validation failed.", details = errors });

            // --- Build the order: server generates the id and timestamp ---
            var order = new Order
            {
                OrderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                CustomerName = request.CustomerName!.Trim(),
                SelectedItemSKUs = request.SelectedItemSKUs!.Select(s => s.Trim()).ToList(),
                TotalPrice = request.TotalPrice!.Value,
                OrderTimestamp = DateTime.UtcNow
            };

            // --- Serialize and push to the queue (503 if storage is unreachable) ---
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient(QueueName);
                await queueClient.CreateIfNotExistsAsync();

                string messageJson = JsonSerializer.Serialize(order);
                await queueClient.SendMessageAsync(messageJson); // Base64-encoded by the client options in Program.cs

                _logger.LogInformation("QueueOrder: order {OrderId} placed on {Queue}.", order.OrderId, QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QueueOrder: failed to place order on queue.");
                return new ObjectResult(new { error = "Could not queue the order. Please try again." })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
            }

          
            return new ObjectResult(new
            {
                message = "Order successfully queued!",
                orderId = order.OrderId,
                orderTimestamp = order.OrderTimestamp
            })
            { StatusCode = StatusCodes.Status202Accepted };
        }

     
        [Function("ProcessOrderQueue")]
        public async Task ProcessOrderQueue(
            [QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] string queueMessage)
        {
            _logger.LogInformation("ProcessOrderQueue: message received: {Message}", queueMessage);

           
            Order? order;
            try
            {
                order = JsonSerializer.Deserialize<Order>(queueMessage, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "ProcessOrderQueue: message is not valid JSON; it will go to the poison queue.");
                throw;
            }

            if (order is null || string.IsNullOrWhiteSpace(order.OrderId) || string.IsNullOrWhiteSpace(order.CustomerName))
            {
                _logger.LogError("ProcessOrderQueue: message is missing OrderId/CustomerName; it will go to the poison queue.");
                throw new InvalidOperationException("Invalid order message: OrderId and CustomerName are required.");
            }

            var tableClient = _tableServiceClient.GetTableClient(TableName);
            await tableClient.CreateIfNotExistsAsync();

            
            string orderDate = order.OrderTimestamp.ToUniversalTime().ToString("yyyy-MM-dd");

            var entity = new TableEntity(orderDate, order.OrderId)
            {
                { "CustomerName", order.CustomerName },
                { "SelectedItemSKUs", string.Join(",", order.SelectedItemSKUs) },
                { "TotalPrice", (double)order.TotalPrice }, 
                { "OrderTimestamp", order.OrderTimestamp },
                { "Status", OrderStatus.Received },
                { "LastUpdated", DateTime.UtcNow }
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            _logger.LogInformation("Order {OrderId} saved with status {Status}.", order.OrderId, OrderStatus.Received);

        
            foreach (var status in new[] { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Collected })
            {
                await Task.Delay(StatusDelay);
                entity["Status"] = status;
                entity["LastUpdated"] = DateTime.UtcNow;
                await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
                _logger.LogInformation("Order {OrderId} status -> {Status}.", order.OrderId, status);
            }
        }

      
        [Function("GetAllOrders")]
        public async Task<IActionResult> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequest req)
        {
            var tableClient = _tableServiceClient.GetTableClient(TableName);
            await tableClient.CreateIfNotExistsAsync();

            var orders = new List<object>();
            await foreach (var e in tableClient.QueryAsync<TableEntity>())
                orders.Add(ToOrderView(e));

            return new OkObjectResult(orders);
        }

        /// <summary>GET /api/orders/status/{orderId} : current status of a single order (404 if unknown).</summary>
        [Function("GetOrderStatus")]
        public async Task<IActionResult> GetOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/status/{orderId}")] HttpRequest req,
            string orderId)
        {
            var tableClient = _tableServiceClient.GetTableClient(TableName);
            await tableClient.CreateIfNotExistsAsync();

            // Partition is the order date, so look up by RowKey across partitions
            await foreach (var e in tableClient.QueryAsync<TableEntity>(x => x.RowKey == orderId))
                return new OkObjectResult(ToOrderView(e));

            return new NotFoundObjectResult(new { error = $"Order '{orderId}' not found (it may still be waiting in the queue)." });
        }

        /// <summary>GET /api/orders/poison : peeks at messages that failed processing.</summary>
        [Function("GetPoisonMessages")]
        public async Task<IActionResult> GetPoisonMessages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/poison")] HttpRequest req)
        {
            var poisonQueue = _queueServiceClient.GetQueueClient(PoisonQueueName);
            if (!await poisonQueue.ExistsAsync())
                return new OkObjectResult(new { count = 0, messages = Array.Empty<string>() });

            var peeked = await poisonQueue.PeekMessagesAsync(maxMessages: 10);
            var messages = peeked.Value.Select(m => m.MessageText).ToList();
            return new OkObjectResult(new { count = messages.Count, messages });
        }

        private static object ToOrderView(TableEntity e) => new
        {
            OrderDate = e.PartitionKey,
            OrderId = e.RowKey,
            CustomerName = e.GetString("CustomerName"),
            SelectedItemSKUs = e.GetString("SelectedItemSKUs"),
            TotalPrice = e.GetDouble("TotalPrice"),
            Status = e.GetString("Status"),
            LastUpdated = e.GetDateTimeOffset("LastUpdated")
        };
    }
}