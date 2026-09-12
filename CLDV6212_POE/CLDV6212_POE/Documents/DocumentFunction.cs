using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace CLDV6212_POE.Documents
{
    public class DocumentFunction
    {
        private readonly ILogger<DocumentFunction> _logger;
        private readonly string _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        private const string ShareName = "staff-docs";

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
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/upload")] HttpRequestData req)
        {
            _logger.LogInformation("Processing staff document upload to File Share.");

            var shareClient = GetShareClient();
            var directoryClient = shareClient.GetRootDirectoryClient();

            // Extract file name from header or use default query/header parameter
            string fileName = "uploaded_document.pdf";
            if (req.Headers.TryGetValues("x-file-name", out var headerValues))
            {
                foreach (var val in headerValues)
                {
                    fileName = val;
                    break;
                }
            }

            var fileClient = directoryClient.GetFileClient(fileName);

            using (var stream = req.Body)
            {
                stream.Position = 0;
                await fileClient.CreateAsync(stream.Length);
                await fileClient.UploadRangeAsync(new Azure.HttpRange(0, stream.Length), stream);
            }

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { message = "Staff document uploaded successfully to File Share!", fileName });
            return response;
        }

        [Function("ListStaffDocuments")]
        public async Task<HttpResponseData> ListStaffDocuments(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documents")] HttpRequestData req)
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
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
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

            await download.Value.Content.CopyToAsync(response.Body);
            return response;
        }
    }
}