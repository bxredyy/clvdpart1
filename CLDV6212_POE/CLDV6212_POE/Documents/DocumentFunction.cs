using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CLDV6212_POE.Documents
{
    public class DocumentFunction
    {
        private readonly ILogger<DocumentFunction> _logger;

        // Azurite does not emulate Azure File Shares (blob/queue/table only), so this must
        // point at a real Azure Storage Account. Table storage still uses AzureWebJobsStorage/Azurite.
        private readonly string _connectionString = Environment.GetEnvironmentVariable("StaffDocsFileShareStorage")
            ?? throw new InvalidOperationException("StaffDocsFileShareStorage is not configured in local.settings.json / app settings.");

        private const string ShareName = "staff-docs";

        // Azure Files rejects any single UploadRange call larger than 4 MiB
        // (HTTP 413 / "InvalidHeaderValue"), so uploads must be chunked.
        private const int MaxRangeBytes = 4 * 1024 * 1024;

        public DocumentFunction(ILogger<DocumentFunction> logger)
        {
            _logger = logger;
        }

        private ShareClient GetShareClient()
        {
            var shareClient = new ShareClient(_connectionString, ShareName);
            shareClient.CreateIfNotExists();
            return shareClient;
        }

        [Function("UploadStaffDocument")]
        public async Task<HttpResponseData> UploadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req)
        {
            _logger.LogInformation("Processing staff document upload to File Share.");

            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing Content-Type header. Send this request as multipart/form-data.");
                return bad;
            }

            string contentType = contentTypeValues.First();
            if (!contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Request must be multipart/form-data with a file field.");
                return bad;
            }

            string? boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary).Value;
            if (string.IsNullOrEmpty(boundary))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Could not determine multipart boundary.");
                return bad;
            }

            string? uploadedFileName = null;

            try
            {
                var reader = new MultipartReader(boundary, req.Body);
                MultipartSection? section;

                var shareClient = GetShareClient();
                var directoryClient = shareClient.GetRootDirectoryClient();

                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var contentDisposition = section.GetContentDispositionHeader();
                    if (contentDisposition == null || !contentDisposition.IsFileDisposition())
                    {
                        continue;
                    }

                    string fileName = contentDisposition.FileName.Value ?? "uploaded_document.pdf";

                    using var memoryStream = new MemoryStream();
                    await section.Body.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var fileClient = directoryClient.GetFileClient(fileName);
                    await fileClient.CreateAsync(memoryStream.Length);

                    long offset = 0;
                    while (offset < memoryStream.Length)
                    {
                        int chunkSize = (int)Math.Min(MaxRangeBytes, memoryStream.Length - offset);
                        using var chunk = new MemoryStream(memoryStream.GetBuffer(), (int)offset, chunkSize, writable: false);
                        await fileClient.UploadRangeAsync(new Azure.HttpRange(offset, chunkSize), chunk);
                        offset += chunkSize;
                    }

                    uploadedFileName = fileName;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload staff document.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync("Failed to upload document to the file share.");
                return error;
            }

            if (uploadedFileName == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("No file field found in the multipart/form-data request.");
                return bad;
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { message = "Staff document uploaded successfully to File Share!", fileName = uploadedFileName });
            return response;
        }

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req)
        {
            _logger.LogInformation("Retrieving list of staff documents from File Share.");

            var shareClient = GetShareClient();
            var directoryClient = shareClient.GetRootDirectoryClient();

            var documents = new List<object>();

            await foreach (ShareFileItem item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    var fileClient = directoryClient.GetFileClient(item.Name);
                    var properties = await fileClient.GetPropertiesAsync();

                    documents.Add(new
                    {
                        FileName = item.Name,
                        FileSize = properties.Value.ContentLength,
                        LastModified = properties.Value.LastModified
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(documents);
            return response;
        }

        [Function("DownloadStaffDocument")]
        public async Task<HttpResponseData> DownloadStaffDocument(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
            string fileName)
        {
            _logger.LogInformation($"Downloading document from File Share: {fileName}");

            var shareClient = GetShareClient();
            var directoryClient = shareClient.GetRootDirectoryClient();
            var fileClient = directoryClient.GetFileClient(fileName);

            if (!await fileClient.ExistsAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Document '{fileName}' not found.");
                return notFound;
            }

            var download = await fileClient.DownloadAsync();
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/octet-stream");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");

            await download.Value.Content.CopyToAsync(response.Body);
            return response;
        }
    }
}
