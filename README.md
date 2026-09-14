# CoffeeNChill - CLDV6212 POE Part 1

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

## Team contributions (Part 1)

| Member | Contribution |
|---|---|
| Lusanda | Initial Azure Functions project: Menu Table Storage CRUD and Documents File Share endpoints |
| _(teammate)_ | _(fill in)_ |
| _(you)_ | Fixed multipart upload data corruption and Azure Files 4MiB chunking bug; fixed Menu/Documents auth-level inconsistency; fixed `UpdateMenuItem` wiping Name/Description on partial updates; built and verified standalone Docker containers; pushed images to Docker Hub; built and verified the Postman collection; wrote this README |

## Demo video

[INSERT UNLISTED YOUTUBE LINK]

## AI use disclosure

Claude Code was used throughout this part to help debug issues (a multipart-parsing bug, an Azure Files upload size limit, an auth-level inconsistency surfaced by container testing, and an entity-update bug found while building the Postman tests) and to assist with Docker/WSL environment setup. All fixes were reviewed, tested, and verified against real Azure resources before being committed.
