using System;
using System.Collections.Generic;
using System.Text;
using Azure;
using Azure.Data.Tables;

namespace CLDV6212_POE.Models
{
    internal class MenuItemEntity : ITableEntity
    {
        // PartitionKey will be the category (e.g., "Hot Drinks", "Pastries")
        public string PartitionKey { get; set; } = string.Empty;

        // RowKey will be the unique item SKU/ID (e.g., "COF-001")
        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
    }
}

