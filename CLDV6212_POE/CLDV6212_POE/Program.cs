using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Azure Tables (MenuItems, Orders)
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException(
            "AzureWebJobsStorage connection string is not configured.");
    return new TableServiceClient(connectionString);
});

// Azure Queues. Base64 encoding is required because the Functions queue trigger
// expects Base64 messages; without it, messages would land in the poison queue.
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException(
            "AzureWebJobsStorage connection string is not configured.");
    return new QueueServiceClient(
        connectionString,
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
});

builder.Build().Run();