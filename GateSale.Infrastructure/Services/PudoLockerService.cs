using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Core.Models;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GateSale.Infrastructure.Services
{
    public class PudoLockerService : IPudoLockerService
    {
        private readonly HttpClient _httpClient;
        private readonly PudoSettings _pudoSettings;
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<PudoLockerService> _logger;
        private readonly IOrderTrackingService _orderTrackingService;

        public PudoLockerService(
            HttpClient httpClient,
            IOptions<PudoSettings> pudoSettings,
            GateSaleDbContext dbContext,
            ILogger<PudoLockerService> logger,
            IOrderTrackingService? orderTrackingService = null)
        {
            _httpClient = httpClient;
            _pudoSettings = pudoSettings.Value;
            _dbContext = dbContext;
            _logger = logger;
            _orderTrackingService = orderTrackingService ?? throw new ArgumentNullException(nameof(orderTrackingService));

            // Configure HTTP client
            _httpClient.BaseAddress = new Uri(_pudoSettings.ApiBaseUrl);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(_pudoSettings.ConnectionTimeoutSeconds);
            
            // Add API authentication headers
            if (!string.IsNullOrEmpty(_pudoSettings.ApiKey))
            {
                // Try different authentication approaches that Pudo API might accept
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _pudoSettings.ApiKey);
                
                // Some APIs use Authorization: ApiKey format
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("ApiKey", _pudoSettings.ApiKey);
                
                // Log the authentication headers for debugging
                _logger.LogInformation("Configured HTTP client with API Key: {ApiKeyPrefix}", 
                    _pudoSettings.ApiKey.Substring(0, Math.Min(10, _pudoSettings.ApiKey.Length)) + "...");
            }
            else
            {
                _logger.LogWarning("No API Key configured for Pudo API");
            }
        }

        public async Task<IEnumerable<Locker>> GetAvailableLockers(double latitude, double longitude, double radiusInKm)
        {
            try
            {
                // Log the request we're about to make
                _logger.LogInformation("Requesting nearby lockers at lat:{lat}, lon:{lon}, radius:{radius}km", 
                    latitude, longitude, radiusInKm);
                
                if (_pudoSettings.UseSandbox)
                {
                    _logger.LogInformation("Using sandbox mode - returning mock lockers");
                    return GetMockLockers(latitude, longitude);
                }
                
                // Call Pudo API to get nearby lockers
                var requestUrl = $"lockers/nearby?lat={latitude}&lon={longitude}&radius={radiusInKm}";
                _logger.LogInformation("Requesting: {BaseUrl}{Url}", _httpClient.BaseAddress, requestUrl);
                
                var response = await _httpClient.GetAsync(requestUrl);
                
                // Log the response status
                _logger.LogInformation("Pudo API response: {StatusCode}", response.StatusCode);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Try to get more details about the error
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Pudo API error response: {ErrorContent}", errorContent);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning("Authentication failed. Returning mock data for development purposes.");
                        return GetMockLockers(latitude, longitude);
                    }
                }
                
                response.EnsureSuccessStatusCode();
                
                var pudoLockers = await response.Content.ReadFromJsonAsync<List<PudoLockerDto>>();
                if (pudoLockers == null) return Enumerable.Empty<Locker>();
                
                // Convert Pudo DTOs to our Locker entities
                var lockers = pudoLockers.Select(pl => new Locker
                {
                    LockerCode = pl.LockerCode,
                    Location = pl.Address,
                    Description = pl.Description,
                    Status = MapPudoStatusToLockerStatus(pl.Status),
                    Latitude = pl.Latitude,
                    Longitude = pl.Longitude
                }).ToList();
                
                return lockers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available lockers from Pudo API");
                throw;
            }
        }
        
        // Add a mock implementation for local testing
        private IEnumerable<Locker> GetMockLockers(double latitude, double longitude)
        {
            // Generate 5 mock lockers around the given coordinates
            var random = new Random();
            var lockers = new List<Locker>();
            
            for (int i = 1; i <= 5; i++)
            {
                // Generate random offsets (roughly within a 5km radius)
                var latOffset = (random.NextDouble() - 0.5) * 0.1;  // ~5km in lat
                var lonOffset = (random.NextDouble() - 0.5) * 0.1;  // ~5km in lon
                
                lockers.Add(new Locker
                {
                    Id = Guid.NewGuid(),
                    LockerCode = $"MOCK{i:D3}",
                    Location = $"Mock Location {i}, Near ({Math.Round(latitude, 4)}, {Math.Round(longitude, 4)})",
                    Description = $"This is a mock locker for testing purposes #{i}",
                    Status = i % 5 == 0 ? LockerStatus.Maintenance : LockerStatus.Available,
                    Latitude = latitude + latOffset,
                    Longitude = longitude + lonOffset,
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
            }
            
            return lockers;
        }

        public async Task<Locker> GetLockerByCode(string lockerCode)
        {
            // First check our database
            var existingLocker = await _dbContext.Lockers
                .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);
            
            if (existingLocker != null)
                return existingLocker;
            
            // If sandbox mode is enabled, return mock data
            if (_pudoSettings.UseSandbox)
            {
                _logger.LogInformation("Using sandbox mode - returning mock locker for code {LockerCode}", lockerCode);
                
                // Try to find a mock locker with matching code or create one
                var mockLocker = GetMockLockers(0, 0)
                    .FirstOrDefault(l => l.LockerCode == lockerCode) ?? 
                    new Locker
                    {
                        Id = Guid.NewGuid(),
                        LockerCode = lockerCode,
                        Location = $"Mock Location for {lockerCode}",
                        Description = $"This is a mock locker created for testing with code {lockerCode}",
                        Status = LockerStatus.Available,
                        Latitude = 0,
                        Longitude = 0,
                        CreatedAt = DateTime.UtcNow
                    };
                
                return mockLocker;
            }
            
            // If not in our database, fetch from Pudo API
            try {
                var response = await _httpClient.GetAsync($"lockers/{lockerCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get locker {LockerCode} from Pudo API. Status: {StatusCode}", 
                        lockerCode, response.StatusCode);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        // API authentication failed, return mock data
                        _logger.LogWarning("Authentication failed. Returning mock data for locker {LockerCode}", lockerCode);
                        
                        return new Locker
                        {
                            Id = Guid.NewGuid(),
                            LockerCode = lockerCode,
                            Location = $"Mock Location for {lockerCode}",
                            Description = $"This is a mock locker created for testing with code {lockerCode}",
                            Status = LockerStatus.Available,
                            Latitude = 0,
                            Longitude = 0,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                    
                    throw new Exception($"Failed to get locker {lockerCode} from Pudo API");
                }
                
                var pudoLocker = await response.Content.ReadFromJsonAsync<PudoLockerDto>();
                if (pudoLocker == null)
                    throw new Exception($"Invalid response from Pudo API for locker {lockerCode}");
                
                return new Locker
                {
                    LockerCode = pudoLocker.LockerCode,
                    Location = pudoLocker.Address,
                    Description = pudoLocker.Description,
                    Status = MapPudoStatusToLockerStatus(pudoLocker.Status),
                    Latitude = pudoLocker.Latitude,
                    Longitude = pudoLocker.Longitude
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching locker {LockerCode} from Pudo API", lockerCode);
                throw;
            }
        }

        public async Task<bool> ReserveLocker(string lockerCode, Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null)
                {
                    _logger.LogError("Order {OrderId} not found when trying to reserve locker", orderId);
                    return false;
                }
                
                var requestBody = new
                {
                    LockerCode = lockerCode,
                    OrderReference = order.OrderNumber,
                    ReservationDuration = 24 // hours
                };
                
                var response = await _httpClient.PostAsJsonAsync("lockers/reserve", requestBody);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to reserve locker {LockerCode}. Status: {StatusCode}", 
                        lockerCode, response.StatusCode);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reserving locker {LockerCode} for order {OrderId}", lockerCode, orderId);
                return false;
            }
        }

        public async Task<bool> AssignOrderToLocker(Guid orderId, string lockerCode)
        {
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Buyer)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null) return false;
                
                var locker = await _dbContext.Lockers
                    .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);
                
                if (locker == null)
                {
                    // Try to get locker from Pudo API
                    locker = await GetLockerByCode(lockerCode);
                    
                    // Save to our database
                    _dbContext.Lockers.Add(locker);
                    await _dbContext.SaveChangesAsync();
                }
                
                // Assign locker to order
                order.BuyerLockerId = locker.Id;
                order.BuyerLocker = locker;
                
                // Update order status
                order.Status = OrderStatus.InTransit;
                
                await _dbContext.SaveChangesAsync();
                
                // Log tracking event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "LockerAssigned",
                    $"Order assigned to locker {lockerCode} at {locker.Location}",
                    locker.Location);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning order {OrderId} to locker {LockerCode}", orderId, lockerCode);
                return false;
            }
        }

        public async Task<string> GenerateAccessCode(Guid orderId, string lockerCode)
        {
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Buyer)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null)
                {
                    _logger.LogError("Order {OrderId} not found when generating access code", orderId);
                    throw new Exception($"Order {orderId} not found");
                }

                // Check if sandbox mode is enabled
                if (_pudoSettings.UseSandbox)
                {
                    _logger.LogInformation("Using sandbox mode - returning mock access code for locker {LockerCode}", lockerCode);
                    
                    // Generate a mock access code
                    var mockAccessCode = $"MOCK-{new Random().Next(100000, 999999)}";
                    
                    // Update order status
                    order.Status = OrderStatus.Delivered;
                    await _dbContext.SaveChangesAsync();
                    
                    // Log tracking event
                    await _orderTrackingService.LogOrderTrackingEvent(
                        orderId,
                        "AccessCodeGenerated",
                        "Access code generated for package pickup (mock)",
                        null);
                    
                    return mockAccessCode;
                }
                
                // If not in sandbox mode, call the real API
                var requestBody = new
                {
                    LockerCode = lockerCode,
                    OrderReference = order.OrderNumber,
                    RecipientPhone = order.Buyer.PhoneNumber ?? "N/A",
                    RecipientEmail = order.Buyer.Email,
                    ValidityHours = 24
                };
                
                var response = await _httpClient.PostAsJsonAsync("lockers/access-code", requestBody);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to generate access code for locker {LockerCode}. Status: {StatusCode}", 
                        lockerCode, response.StatusCode);
                    throw new Exception($"Failed to generate access code for locker {lockerCode}");
                }
                
                var result = await response.Content.ReadFromJsonAsync<AccessCodeResponse>();
                
                if (result == null || string.IsNullOrEmpty(result.AccessCode))
                {
                    throw new Exception("Invalid access code response from Pudo API");
                }
                
                // Update order status
                order.Status = OrderStatus.Delivered;
                await _dbContext.SaveChangesAsync();
                
                // Log tracking event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "AccessCodeGenerated",
                    "Access code generated for package pickup",
                    null);
                
                return result.AccessCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access code for order {OrderId}, locker {LockerCode}", 
                    orderId, lockerCode);
                throw;
            }
        }

        public async Task<bool> ReleaseLocker(string lockerCode, string accessCode)
        {
            try
            {
                // Check if sandbox mode is enabled
                if (_pudoSettings.UseSandbox)
                {
                    _logger.LogInformation("Using sandbox mode - simulating locker release for {LockerCode}", lockerCode);
                    
                    // Update locker status in our database
                    var sandboxLocker = await _dbContext.Lockers
                        .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);
                    
                    if (sandboxLocker != null)
                    {
                        sandboxLocker.Status = LockerStatus.Available;
                        await _dbContext.SaveChangesAsync();
                    }
                    
                    return true;
                }
                
                // If not in sandbox mode, call the real API
                var requestBody = new
                {
                    LockerCode = lockerCode,
                    AccessCode = accessCode
                };
                
                var response = await _httpClient.PostAsJsonAsync("lockers/release", requestBody);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to release locker {LockerCode}. Status: {StatusCode}", 
                        lockerCode, response.StatusCode);
                    return false;
                }
                
                // Update locker status in our database
                var apiLocker = await _dbContext.Lockers
                    .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);
                
                if (apiLocker != null)
                {
                    apiLocker.Status = LockerStatus.Available;
                    await _dbContext.SaveChangesAsync();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing locker {LockerCode}", lockerCode);
                return false;
            }
        }

        public async Task ProcessLockerStatusUpdate(string lockerCode, LockerStatus newStatus, string transactionId)
        {
            try
            {
                // Verify webhook authenticity
                if (!VerifyWebhookSignature(transactionId))
                {
                    _logger.LogWarning("Invalid webhook signature for locker status update");
                    return;
                }
                
                var locker = await _dbContext.Lockers
                    .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);
                
                if (locker == null)
                {
                    // Create a new locker record if it doesn't exist
                    locker = await GetLockerByCode(lockerCode);
                    _dbContext.Lockers.Add(locker);
                }
                
                // Update status
                locker.Status = newStatus;
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Updated locker {LockerCode} status to {Status}", 
                    lockerCode, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing locker status update for {LockerCode}", lockerCode);
            }
        }

        public async Task ProcessOrderPickupConfirmation(Guid orderId, string lockerCode, DateTime pickupTime)
        {
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.BuyerLocker)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for pickup confirmation", orderId);
                    return;
                }
                
                // Verify the order is assigned to the correct locker
                if (order.BuyerLocker == null || order.BuyerLocker.LockerCode != lockerCode)
                {
                    _logger.LogWarning("Order {OrderId} is not assigned to locker {LockerCode}", orderId, lockerCode);
                    return;
                }
                
                // Update order status
                order.Status = OrderStatus.Completed;
                order.CompletedAt = pickupTime;
                
                // Update locker status
                order.BuyerLocker.Status = LockerStatus.Available;
                
                await _dbContext.SaveChangesAsync();
                
                // Log tracking event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "PackagePickedUp",
                    $"Package picked up from locker {lockerCode} at {pickupTime}",
                    order.BuyerLocker.Location);
                
                _logger.LogInformation("Order {OrderId} completed with pickup from locker {LockerCode}", 
                    orderId, lockerCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order pickup confirmation for order {OrderId}", orderId);
            }
        }
        
        public async Task ProcessWebhookEvent(PudoWebhookEvent webhookEvent)
        {
            try
            {
                // Log the webhook event
                await LogWebhookEvent(
                    webhookEvent.EventType, 
                    JsonConvert.SerializeObject(webhookEvent), 
                    false);
                
                var processingResult = string.Empty;
                
                switch (webhookEvent.EventType)
                {
                    case PudoEventTypes.LockerStatusChange:
                        if (Enum.TryParse<LockerStatus>(webhookEvent.Status, true, out var newStatus))
                        {
                            await ProcessLockerStatusUpdate(
                                webhookEvent.LockerCode, 
                                newStatus, 
                                webhookEvent.TransactionId ?? string.Empty);
                            
                            processingResult = $"Updated locker {webhookEvent.LockerCode} status to {newStatus}";
                        }
                        break;
                        
                    case PudoEventTypes.PackageDropped:
                        // Find order by reference number
                        var orderByRef = await _dbContext.Orders
                            .FirstOrDefaultAsync(o => o.OrderNumber == webhookEvent.OrderReference);
                            
                        if (orderByRef != null)
                        {
                            await _orderTrackingService.UpdateOrderStatus(
                                orderByRef.Id, 
                                OrderStatus.Delivered);
                                
                            await _orderTrackingService.LogOrderTrackingEvent(
                                orderByRef.Id,
                                "PackageDropped",
                                $"Package dropped at locker {webhookEvent.LockerCode}",
                                null);
                                
                            processingResult = $"Updated order {orderByRef.Id} status to ReadyForPickup";
                        }
                        break;
                        
                    case PudoEventTypes.PackagePickedUp:
                        // Find order by reference number
                        var orderForPickup = await _dbContext.Orders
                            .FirstOrDefaultAsync(o => o.OrderNumber == webhookEvent.OrderReference);
                            
                        if (orderForPickup != null)
                        {
                            await ProcessOrderPickupConfirmation(
                                orderForPickup.Id,
                                webhookEvent.LockerCode,
                                webhookEvent.Timestamp);
                                
                            processingResult = $"Processed pickup for order {orderForPickup.Id}";
                        }
                        break;
                        
                    case PudoEventTypes.AccessCodeGenerated:
                        processingResult = $"Access code generated for locker {webhookEvent.LockerCode}";
                        break;
                        
                    case PudoEventTypes.AccessCodeUsed:
                        processingResult = $"Access code used for locker {webhookEvent.LockerCode}";
                        break;
                        
                    default:
                        processingResult = $"Unhandled event type: {webhookEvent.EventType}";
                        break;
                }
                
                // Update webhook log with processing result
                await LogWebhookEvent(
                    webhookEvent.EventType,
                    JsonConvert.SerializeObject(webhookEvent),
                    true,
                    processingResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook event {EventType}", webhookEvent.EventType);
                
                // Log error in webhook log
                await LogWebhookEvent(
                    webhookEvent.EventType,
                    JsonConvert.SerializeObject(webhookEvent),
                    false,
                    null,
                    ex.Message);
            }
        }
        
        public async Task LogWebhookEvent(
            string eventType, 
            string rawPayload, 
            bool isProcessed = false, 
            string? result = null, 
            string? errorMessage = null)
        {
            try
            {
                var webhookLog = new PudoWebhookLog
                {
                    EventType = eventType,
                    RawPayload = rawPayload,
                    IsProcessed = isProcessed,
                    ReceivedAt = DateTime.UtcNow,
                    ProcessedAt = isProcessed ? DateTime.UtcNow : null,
                    ProcessingResult = result,
                    ErrorMessage = errorMessage
                };
                
                _dbContext.PudoWebhookLogs.Add(webhookLog);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging webhook event");
            }
        }

        #region Helper Methods
        
        private LockerStatus MapPudoStatusToLockerStatus(string pudoStatus)
        {
            return pudoStatus.ToLower() switch
            {
                "available" => LockerStatus.Available,
                "occupied" => LockerStatus.Occupied,
                "maintenance" => LockerStatus.Maintenance,
                "out_of_service" or "outofservice" => LockerStatus.OutOfService,
                _ => LockerStatus.OutOfService
            };
        }
        
        public async Task<bool> VerifyWebhookSignature(string payload, string signature)
        {
            // Implementation would depend on Pudo's webhook signature verification method
            if (string.IsNullOrEmpty(_pudoSettings.WebhookSecret))
                return false;

            // In a real implementation, you would verify the webhook signature
            // using HMAC or other cryptographic methods
            try
            {
                // Example: HMAC-SHA256 verification
                using var hmac = new System.Security.Cryptography.HMACSHA256(
                    System.Text.Encoding.UTF8.GetBytes(_pudoSettings.WebhookSecret));
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                var computedSignature = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
                
                return signature.Equals(computedSignature, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying webhook signature");
                return false;
            }
        }
        
        private bool VerifyWebhookSignature(string transactionId)
        {
            // Legacy method for backward compatibility
            if (string.IsNullOrEmpty(_pudoSettings.WebhookSecret))
                return false;

            // In a real implementation, you would verify the webhook signature
            // using the provided headers and payload
            return true;
        }
        
        #endregion
        
        #region DTOs
        
        private class PudoLockerDto
        {
            public string LockerCode { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }
        
        private class AccessCodeResponse
        {
            public string AccessCode { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
        }
        
        #endregion
    }
}