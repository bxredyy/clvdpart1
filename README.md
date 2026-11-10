# CoffeeNChill - CLDV6212 POE Parts 1 and 2

CoffeeNChill is a canteen application built using Azure Functions and Azure Storage. The project supports menu management, staff documents and background order processing. Docker provides the container setup, and Postman is used to test the API.

## Project features

### Menu management

Menu items are stored in the MenuItems table. The API supports creating items, listing the menu, filtering by category, updating items and deleting items.

### Staff documents

Staff documents are stored in the staff-docs Azure File Share. The API supports uploading, listing and downloading documents. Azure Files requires a real Azure Storage account because Azurite does not emulate file shares.

### Order processing

The order endpoint accepts a customer name, selected item SKUs and total price. It validates the request and generates an order ID and timestamp. The order is serialized as JSON and sent as a Base64 encoded message to order-processing-queue.

A queue-triggered Function reads the message and stores the order in the Orders table. The order date is the partition key and the order ID is the row key. The worker updates the status through Received, Preparing, Ready and Collected.

The API also provides order listing, status lookup and poison queue inspection.

## Repository structure

- CLDV6212_POE/CLDV6212_POE: Functions project used for the containerized API.
- CLDV6212_POE/CLDV6212_POE.slnx: solution file.
- docker-compose.yml: local Functions and Azurite setup.
- compose.azure.yml: optional override for cloud order storage.
- .env.example: example environment variables.
- docs: Postman collections, environment and sample upload file.

## Requirements

- Docker Desktop running with Linux containers.
- An active Azure Storage account for staff documents.
- Postman for API testing.
- .NET SDK 10 and Azure Functions Core Tools v4 for running the Functions project outside Docker.

## Docker setup

Part 1 uses standalone containers. Part 2 uses Docker Compose to start the Functions application and Azurite together.

Run the following from the repository root in Command Prompt:

    if not exist .env copy .env.example .env

Set STAFF_DOCS_CONNECTION_STRING in .env to the connection string for the staff document file share. Keep .env private; it is ignored by Git.

Start the application:

    docker compose -f docker-compose.yml up -d --pull always
    docker compose -f docker-compose.yml ps

The API is available at http://localhost:7071/api.

The default Compose setup uses Azurite for local Blob, Queue and Table storage. Both containers share a bridge network. A health check waits for Azurite before starting Functions, and a named volume preserves storage data when containers are recreated.

The optional compose.azure.yml override switches order storage to the real storage account configured through STAFF_DOCS_CONNECTION_STRING. It is not required for the default local Azurite workflow.

## Published Part 2 images

- zgotem/coffeenchill-functions:v2.0
- zgotem/coffeenchill-azurite:v2.0

Pull the images directly:

    docker pull zgotem/coffeenchill-functions:v2.0
    docker pull zgotem/coffeenchill-azurite:v2.0

## Order endpoints

- POST /api/orders/queue: validate and queue an order.
- GET /api/orders: list saved orders.
- GET /api/orders/status/{orderId}: retrieve an order status.
- GET /api/orders/poison: inspect poison queue messages.

## Testing

Import docs/CoffeeNChill.part2.postman_collection.json into Postman and use http://localhost:7071 as baseUrl. The original collection is in docs/CoffeeNChill.postman_collection.json.

The Part 2 collection covers menu operations, staff documents, order acceptance, lifecycle tracking, completed order retrieval, invalid input, unknown orders and poison queue inspection.

For document uploads, select docs/sample-staff-recipe.txt in the file form-data field. Set the Postman working directory to the repository root if the Runner cannot access the file. Staff document requests require an active Azure Files connection.

Check the response codes and assertions in Postman. Expected responses include 202 for an accepted order, 200 for retrieval, 400 for invalid input and 404 for an unknown order.

## Group demonstration

The group demonstration covers the application features, container setup and testing evidence. The unlisted video link is provided below.

## Demo video

[INSERT UNLISTED YOUTUBE LINK]

## AI use disclosure

Claude Code was used throughout this part to help debug issues (a multipart-parsing bug, an Azure Files upload size limit, an auth-level inconsistency surfaced by container testing, and an entity-update bug found while building the Postman tests) and to assist with Docker/WSL environment setup. All fixes were reviewed, tested, and verified against real Azure resources before being committed.
