    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["AzureWebJobsStorage"]
        ?? throw new InvalidOperationException(
            "AzureWebJobsStorage connection string is not configured.");
    return new TableServiceClient(connectionString);
