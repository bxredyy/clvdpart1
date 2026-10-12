# CoffeeNChill - CLDV6212 POE Parts 1 and 2

Cloud foundation for the CoffeeNChill canteen system: Azure Table Storage for the menu, an Azure File Share for staff documents, and HTTP-triggered Azure Functions, all running locally via Azurite and containerized with Docker.

## What this covers

- **Menu Table Storage** (`MenuItems` table): create, list, list-by-category, update, delete
- **Staff Documents File Share** (`staff-docs`): upload, list, download
- Both containerized as standalone Docker images (no Docker Compose in Part 1)

## Local setup

### Prerequisites
- .NET SDK 10
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local) (`npm install -g azure-functions-core-tools@4 --unsafe-perm true`)
- [Azurite](https://github.com/Azure/Azurite) (`npm install -g azurite`) or Docker
- A real Azure Storage Account for the file share (Azurite does not emulate Azure Files — blob/queue/table only)

### Configure `local.settings.json`

In `CLDV6212_POE/CLDV6212_POE/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "StaffDocsFileShareStorage": "<connection string for a real Azure Storage Account>"
  }
}
```

### Run locally

```bash
# Terminal 1 - start Azurite (Table/Blob/Queue emulation)
azurite --location .azurite

# Terminal 2 - start the Functions host
cd CLDV6212_POE/CLDV6212_POE
func start
```

The API is then available at `http://localhost:7071/api/...`.

## Running with Docker (standalone containers)

Per the Part 1 spec, no Docker Compose — each component runs as its own container.

**1. Run Azurite in its own container:**
```bash
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

**2. Build the Functions image:**
```bash
cd CLDV6212_POE
docker build -f CLDV6212_POE/Dockerfile -t <dockerhub_username>/coffeenchill-functions:v1.0 .
```

**3. Run the Functions container**, pointed at the Azurite container for table storage and a real Azure Storage connection string for the file share:
```bash
docker network create coffeenchill-net
docker network connect coffeenchill-net azurite

docker run -d --name coffeenchill-functions --network coffeenchill-net -p 7071:80 \
  -e "AzureWebJobsStorage=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;TableEndpoint=http://azurite:10002/devstoreaccount1;" \
  -e "StaffDocsFileShareStorage=<connection string for a real Azure Storage Account>" \
  <dockerhub_username>/coffeenchill-functions:v1.0
```

Published images:
- `docker pull <dockerhub_username>/coffeenchill-functions:v1.0`
- `docker pull <dockerhub_username>/coffeenchill-azurite:v1.0`

## Testing

A Postman collection covering all 8 endpoints is in [`docs/CoffeeNChill.postman_collection.json`](docs/CoffeeNChill.postman_collection.json). Import it, set the `baseUrl` variable to your local (`http://localhost:7071`) or containerized (`http://localhost:7072`) instance, and run through it.

## Demo video

[INSERT UNLISTED YOUTUBE LINK]

## AI use disclosure

Claude Code was used throughout this part to help debug issues (a multipart-parsing bug, an Azure Files upload size limit, an auth-level inconsistency surfaced by container testing, and an entity-update bug found while building the Postman tests) and to assist with Docker/WSL environment setup. All fixes were reviewed, tested, and verified against real Azure resources before being committed.

## Part 2: queue-based order processing

Part 2 adds an asynchronous order workflow. The order request is validated and placed on the `order-processing-queue`. The queue-triggered function saves the order in the `Orders` table and updates its status through `Received`, `Preparing`, `Ready`, and `Collected`.

### Run with Docker Compose

Run these commands from the repository root. The `.env` copy only happens if you do not already have a local `.env` file.

```powershell
if (-not (Test-Path .env)) {
    Copy-Item .env.example .env
}
```

Set `STAFF_DOCS_CONNECTION_STRING` in `.env` to the Azure Storage connection string for the `staff-docs` file share. Keep `.env` private; it is ignored by Git.

Start the services:

```powershell
docker compose up -d --pull always
docker compose ps
```

The Functions API is available at `http://localhost:7071/api`. Azurite provides local Queue and Table storage for the Compose setup. Staff documents use the configured Azure File Share.

### Order endpoints

- `POST /api/orders/queue` — validate and queue a new order
- `GET /api/orders` — list saved orders
- `GET /api/orders/status/{orderId}` — check an order's current status
- `GET /api/orders/poison` — inspect messages in the poison queue

The order table uses the order date as the partition key and the order ID as the row key.

### Postman tests

Import [`docs/CoffeeNChill.part2.postman_collection.json`](docs/CoffeeNChill.part2.postman_collection.json) and use the local base URL `http://localhost:7071`.

For the document upload test, Postman must be able to read `docs/sample-staff-recipe.txt`. Set Postman's working directory to the repository root if the Runner cannot find the file.

## Part 2 submission

### Published images

- Functions: zgotem/coffeenchill-functions:v2.0
- Azurite: zgotem/coffeenchill-azurite:v2.0

### Project and startup

The Functions project is in CLDV6212_POE/CLDV6212_POE. Run from the repository root:

    docker compose -f docker-compose.yml up -d --pull always
    docker compose -f docker-compose.yml ps

The API runs at http://localhost:7071/api. The default Compose setup uses Azurite queues and tables, a bridge network, a health check and persistent storage. Staff documents use real Azure Files configured through the ignored local .env file. compose.azure.yml is an optional cloud storage override.

### Testing evidence

The captured Postman run passed 25 of 26 assertions. Order acceptance, completion, retrieval and validation tests passed. The Runner document upload returned 400; a separate manual upload returned 201. Document listing and download returned 200.

Earlier testing screenshots are included in the demonstration. Later cloud operations failed and Azure reported ReadOnlyDisabledSubscription.
