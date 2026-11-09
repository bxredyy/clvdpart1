using Azure.Data.Tables;
using CoffeeNChill.Gateway.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _storageConnectionString;
    private const string TableName = "Users";

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
        _storageConnectionString = configuration.GetConnectionString("AzureStorage") ?? "UseDevelopmentStorage=true";
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var tableClient = new TableClient(_storageConnectionString, TableName);
        await tableClient.CreateIfNotExistsAsync();

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var userEntity = new UserEntity
        {
            PartitionKey = request.Role,
            RowKey = request.Email,
            PasswordHash = passwordHash,
            FullName = request.FullName,
            CreatedDate = DateTime.UtcNow
        };

        await tableClient.AddEntityAsync(userEntity);
        return Ok(new { message = "User registered successfully!" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var tableClient = new TableClient(_storageConnectionString, TableName);
        await tableClient.CreateIfNotExistsAsync();

        UserEntity? user = null;
        await foreach (var entity in tableClient.QueryAsync<UserEntity>(u => u.RowKey == request.Email))
        {
            user = entity;
            break;
        }

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var token = GenerateJwtToken(user);
        return Ok(new { token, role = user.PartitionKey, email = user.RowKey });
    }

    private string GenerateJwtToken(UserEntity user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "SuperSecretLongKeyThatIsAtLeast32BytesLongForCoffeeNChill2026!");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.RowKey),
            new Claim(ClaimTypes.Role, user.PartitionKey)
        };

        var key = new SymmetricSecurityKey(secretKey);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"] ?? "CoffeeNChillGateway",
            audience: jwtSettings["Audience"] ?? "CoffeeNChillUsers",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(3),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}