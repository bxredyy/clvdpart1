using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Models
{
    public class UserEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty; // Role: "Admin", "Staff", or "Customer"
        public string RowKey { get; set; } = string.Empty;     // Email / Username
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer"; // "Admin", "Staff", or "Customer"
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}