# Build the Functions application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore packages before copying the remaining files
COPY ["CLDV6212_POE/CLDV6212_POE.csproj", "CLDV6212_POE/"]
RUN dotnet restore "CLDV6212_POE/CLDV6212_POE.csproj"

# Publish the application
COPY . .
RUN dotnet publish "CLDV6212_POE/CLDV6212_POE.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Run the published application in the Functions host
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS final
WORKDIR /home/site/wwwroot

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated

EXPOSE 80

COPY --from=build /app/publish .
