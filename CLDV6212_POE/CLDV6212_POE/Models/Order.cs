using System;
using System.Collections.Generic;
using System.Text;

namespace CLDV6212_POE.Models
{
   
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public List<string> SelectedItemSKUs { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public DateTime OrderTimestamp { get; set; }
    }

  
    public class OrderRequest
    {
        public string? CustomerName { get; set; }
        public List<string>? SelectedItemSKUs { get; set; }
        public decimal? TotalPrice { get; set; }
    }

    /// <summary>Order lifecycle states: Received -> Preparing -> Ready -> Collected.</summary>
    public static class OrderStatus
    {
        public const string Received = "Received";
        public const string Preparing = "Preparing";
        public const string Ready = "Ready";
        public const string Collected = "Collected";
    }
}