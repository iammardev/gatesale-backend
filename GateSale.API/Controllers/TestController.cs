using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<TestController> _logger;
        private readonly IConfiguration _configuration;

        public TestController(
            GateSaleDbContext dbContext,
            ILogger<TestController> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var orders = await _dbContext.Orders
                    .Select(o => new
                    {
                        o.Id,
                        o.OrderNumber,
                        o.OrderDate,
                        Status = o.Status.ToString(),
                        BuyerId = o.BuyerId,
                        BuyerLockerId = o.BuyerLockerId
                    })
                    .Take(10)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders");
                return StatusCode(500, "An error occurred while retrieving orders");
            }
        }

        [HttpPost("create-test-order")]
        public async Task<IActionResult> CreateTestOrder()
        {
            try
            {
                // Get a user to assign as buyer
                var user = await _dbContext.Users.FirstOrDefaultAsync();
                if (user == null)
                {
                    // Create a test user if none exists
                    user = new User
                    {
                        Username = "testuser",
                        Email = "test@example.com",
                        FullName = "Test User",
                        School = "Test School",
                        Grade = 10,
                        DateOfBirth = DateTime.UtcNow.AddYears(-16),
                        IsMinor = true,
                        Status = UserStatus.Active,
                        IsEmailVerified = true,
                        IsProfileComplete = true,
                        CognitoUserId = "test-cognito-id"
                    };
                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();
                }

                // Create test order
                var order = new Order
                {
                    OrderNumber = $"TEST-{DateTime.UtcNow.Ticks}",
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = 100.00m,
                    Status = OrderStatus.PaidAwaitingShipment,
                    BuyerId = user.Id,
                    Buyer = user
                };

                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    OrderId = order.Id,
                    order.OrderNumber,
                    order.Status,
                    BuyerId = order.BuyerId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test order");
                return StatusCode(500, "An error occurred while creating test order");
            }
        }

        [HttpGet("generate-test-token")]
        public IActionResult GenerateTestToken([FromQuery] Guid? userId = null)
        {
            try
            {
                // Use the provided userId or get the first user from the database
                Guid actualUserId;
                
                if (userId.HasValue)
                {
                    actualUserId = userId.Value;
                }
                else
                {
                    var user = _dbContext.Users.FirstOrDefault();
                    if (user == null)
                    {
                        return NotFound("No users found in the database. Create a test user first.");
                    }
                    actualUserId = user.Id;
                }

                // Create claims for the token
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, actualUserId.ToString()),
                    new Claim(ClaimTypes.Name, "Test User"),
                    new Claim(ClaimTypes.Email, "test@example.com"),
                    new Claim("custom:role", "user")
                };

                // Get JWT settings from configuration
                var issuer = _configuration["Jwt:Issuer"] ?? "https://localhost:5221";
                var audience = _configuration["Jwt:Audience"] ?? "GateSale.API";
                var key = _configuration["Jwt:Key"] ?? "YourSuperSecretKeyForDevelopmentPurposesOnly12345!@#$%";

                // Create token
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
                
                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(24),
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return Ok(new
                {
                    Token = tokenString,
                    ExpiresIn = 86400, // 24 hours in seconds
                    UserId = actualUserId,
                    TokenType = "Bearer"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating test token");
                return StatusCode(500, "An error occurred while generating test token");
            }
        }
    }
}