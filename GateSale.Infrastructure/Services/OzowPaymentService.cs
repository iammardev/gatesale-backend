using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GateSale.Infrastructure.Services
{
    public class OzowPaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly GateSaleDbContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OzowPaymentService> _logger;
        private readonly IOrderService _orderService;
        
        // Ozow API settings
        private readonly string _siteCode;
        private readonly string _privateKey;
        private readonly string _apiKey;
        private readonly string _apiBaseUrl;
        private readonly bool _testMode;
        
        public OzowPaymentService(
            IConfiguration configuration,
            GateSaleDbContext dbContext,
            HttpClient httpClient,
            ILogger<OzowPaymentService> logger,
            IOrderService orderService)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _httpClient = httpClient;
            _logger = logger;
            _orderService = orderService;
            
            // Load Ozow settings from configuration
            _siteCode = _configuration["Ozow:SiteCode"] ?? throw new ArgumentNullException("Ozow:SiteCode");
            _privateKey = _configuration["Ozow:PrivateKey"] ?? throw new ArgumentNullException("Ozow:PrivateKey");
            _apiKey = _configuration["Ozow:ApiKey"] ?? throw new ArgumentNullException("Ozow:ApiKey");
            _apiBaseUrl = _configuration["Ozow:ApiBaseUrl"] ?? throw new ArgumentNullException("Ozow:ApiBaseUrl");
            _testMode = bool.Parse(_configuration["Ozow:TestMode"] ?? "false");
        }
        
        public async Task<PaymentResultDto> InitiatePaymentAsync(Guid orderId, decimal amount, string returnUrl, string cancelUrl)
        {
            try
            {
                // Get order details
                var order = await _dbContext.Orders
                    .Include(o => o.Buyer)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null)
                {
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Order with ID {orderId} not found",
                        PaymentUrl = string.Empty,
                        PaymentReference = string.Empty
                    };
                }
                
                // Generate payment reference
                var paymentReference = $"GSB-{DateTime.UtcNow:yyyyMMdd}-{orderId.ToString().Substring(0, 8)}";
                
                // Prepare Ozow payment request
                var ozowRequest = new
                {
                    SiteCode = _siteCode,
                    CountryCode = "ZA",
                    CurrencyCode = "ZAR",
                    Amount = amount.ToString("F2"),
                    TransactionReference = paymentReference,
                    BankReference = $"GateSale-{order.OrderNumber}",
                    Customer = new
                    {
                        Name = order.Buyer.FullName,
                        Email = order.Buyer.Email,
                        Mobile = order.Buyer.PhoneNumber ?? string.Empty
                    },
                    SuccessUrl = returnUrl,
                    CancelUrl = cancelUrl,
                    NotifyUrl = _configuration["Ozow:DefaultNotifyUrl"] ?? "https://api.gatesale.com/api/payment/webhook",
                    IsTest = _testMode
                };
                
                // Calculate hash signature
                var hashString = $"{_siteCode}{ozowRequest.Amount}{ozowRequest.TransactionReference}{_privateKey}";
                var hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(hashString));
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                // Add hash to request
                var finalRequest = new Dictionary<string, object>(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(ozowRequest)) ?? new Dictionary<string, object>())
                {
                    { "HashCheck", hash }
                };
                
                // Call Ozow API
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/post/paymentrequest", finalRequest);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ozow API error: {ErrorContent}", errorContent);
                    
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = $"Payment gateway error: {response.StatusCode}",
                        PaymentUrl = string.Empty,
                        PaymentReference = paymentReference
                    };
                }
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                var ozowResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                
                if (ozowResponse == null || !ozowResponse.TryGetValue("url", out var paymentUrl))
                {
                    return new PaymentResultDto
                    {
                        Success = false,
                        Message = "Invalid response from payment gateway",
                        PaymentUrl = string.Empty,
                        PaymentReference = paymentReference
                    };
                }
                
                // Create transaction record
                var transaction = new Transaction
                {
                    OrderId = orderId,
                    Amount = amount,
                    PaymentProvider = "Ozow",
                    PaymentMethod = "Redirect",
                    TransactionId = paymentReference,
                    Status = TransactionStatus.Pending
                };
                
                _dbContext.Transactions.Add(transaction);
                await _dbContext.SaveChangesAsync();
                
                return new PaymentResultDto
                {
                    Success = true,
                    PaymentUrl = paymentUrl.ToString() ?? string.Empty,
                    PaymentReference = paymentReference
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating payment for order {OrderId}", orderId);
                
                return new PaymentResultDto
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    PaymentUrl = string.Empty,
                    PaymentReference = string.Empty
                };
            }
        }
        
        public async Task<bool> ValidateWebhookAsync(string payload, string signature)
        {
            try
            {
                // Parse payload
                var payloadData = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
                if (payloadData == null)
                {
                    _logger.LogWarning("Invalid webhook payload format");
                    return false;
                }
                
                // Extract required fields
                if (!payloadData.TryGetValue("SiteCode", out var siteCode) ||
                    !payloadData.TryGetValue("TransactionId", out var transactionId) ||
                    !payloadData.TryGetValue("Amount", out var amount) ||
                    !payloadData.TryGetValue("Status", out var status))
                {
                    _logger.LogWarning("Missing required fields in webhook payload");
                    return false;
                }
                
                // Verify site code
                if (siteCode.ToString() != _siteCode)
                {
                    _logger.LogWarning("Invalid site code in webhook: {SiteCode}", siteCode);
                    return false;
                }
                
                // Calculate expected hash
                var hashString = $"{siteCode}{transactionId}{amount}{status}{_privateKey}";
                var hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(hashString));
                var expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                // Compare hashes
                return signature.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating webhook signature");
                return false;
            }
        }
        
        public async Task<WebhookResultDto> ProcessPaymentWebhookAsync(string payload)
        {
            try
            {
                // Parse payload
                var payloadData = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
                if (payloadData == null)
                {
                    return new WebhookResultDto
                    {
                        Success = false,
                        Message = "Invalid payload format"
                    };
                }
                
                // Extract required fields
                if (!payloadData.TryGetValue("TransactionReference", out var transactionReference) ||
                    !payloadData.TryGetValue("TransactionId", out var transactionId) ||
                    !payloadData.TryGetValue("Status", out var statusObj) ||
                    !payloadData.TryGetValue("Amount", out var amountObj))
                {
                    return new WebhookResultDto
                    {
                        Success = false,
                        Message = "Missing required fields in payload"
                    };
                }
                
                var status = statusObj.ToString();
                var amount = decimal.Parse(amountObj.ToString() ?? "0");
                
                // Find transaction by reference
                var transaction = await _dbContext.Transactions
                    .Include(t => t.Order)
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionReference.ToString());
                
                if (transaction == null)
                {
                    return new WebhookResultDto
                    {
                        Success = false,
                        Message = $"Transaction with reference {transactionReference} not found"
                    };
                }
                
                // Update transaction status based on Ozow status
                switch (status?.ToLower())
                {
                    case "complete":
                        transaction.Status = TransactionStatus.Completed;
                        transaction.ProcessedAt = DateTime.UtcNow;
                        
                        // Update order status
                        await _orderService.UpdateOrderStatusAsync(
                            transaction.OrderId, 
                            OrderStatus.PaidAwaitingShipment, 
                            "Payment completed via Ozow");
                        
                        // Hold payment in escrow
                        await HoldPaymentInEscrowAsync(transaction.OrderId, transaction.Amount);
                        break;
                        
                    case "cancelled":
                        transaction.Status = TransactionStatus.Cancelled;
                        transaction.ProcessedAt = DateTime.UtcNow;
                        break;
                        
                    case "failed":
                        transaction.Status = TransactionStatus.Failed;
                        transaction.ProcessedAt = DateTime.UtcNow;
                        break;
                        
                    default:
                        // Unknown status
                        _logger.LogWarning("Unknown payment status: {Status}", status);
                        break;
                }
                
                await _dbContext.SaveChangesAsync();
                
                return new WebhookResultDto
                {
                    Success = true,
                    OrderReference = transaction.Order.OrderNumber,
                    Amount = transaction.Amount,
                    TransactionId = transactionId.ToString(),
                    Status = status
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment webhook");
                
                return new WebhookResultDto
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        
        public async Task<RefundResultDto> ProcessRefundAsync(Guid orderId, decimal amount, string reason)
        {
            try
            {
                // Get order and transaction details
                var order = await _dbContext.Orders
                    .Include(o => o.Transaction)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null || order.Transaction == null)
                {
                    return new RefundResultDto
                    {
                        Success = false,
                        Message = $"Order with ID {orderId} not found or has no transaction"
                    };
                }
                
                // Generate refund reference
                var refundReference = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                // Prepare Ozow refund request
                var ozowRequest = new
                {
                    SiteCode = _siteCode,
                    OriginalTransactionId = order.Transaction.TransactionId,
                    Amount = amount.ToString("F2"),
                    RefundReference = refundReference,
                    IsTest = _testMode
                };
                
                // Calculate hash signature
                var hashString = $"{_siteCode}{ozowRequest.OriginalTransactionId}{ozowRequest.Amount}{_privateKey}";
                var hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(hashString));
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                // Add hash to request
                var finalRequest = new Dictionary<string, object>(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(ozowRequest)) ?? new Dictionary<string, object>())
                {
                    { "HashCheck", hash }
                };
                
                // Call Ozow API
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/post/refund", finalRequest);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ozow API error: {ErrorContent}", errorContent);
                    
                    return new RefundResultDto
                    {
                        Success = false,
                        Message = $"Payment gateway error: {response.StatusCode}"
                    };
                }
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                var ozowResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                
                if (ozowResponse == null || !ozowResponse.TryGetValue("success", out var successObj) || 
                    !bool.TryParse(successObj.ToString(), out var success) || !success)
                {
                    return new RefundResultDto
                    {
                        Success = false,
                        Message = "Refund request failed",
                        RefundReference = refundReference
                    };
                }
                
                // Create refund transaction record
                var refundTransaction = new Transaction
                {
                    OrderId = orderId,
                    Amount = amount,
                    PaymentProvider = "Ozow",
                    PaymentMethod = "Refund",
                    TransactionId = refundReference,
                    Status = TransactionStatus.Completed,
                    ProcessedAt = DateTime.UtcNow
                };
                
                _dbContext.Transactions.Add(refundTransaction);
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.Refunded, 
                    $"Refund processed: {reason}");
                
                await _dbContext.SaveChangesAsync();
                
                return new RefundResultDto
                {
                    Success = true,
                    RefundReference = refundReference,
                    Amount = amount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for order {OrderId}", orderId);
                
                return new RefundResultDto
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
        
        public async Task<bool> VerifyPaymentStatusAsync(string paymentReference)
        {
            try
            {
                // Prepare Ozow status check request
                var ozowRequest = new
                {
                    SiteCode = _siteCode,
                    TransactionReference = paymentReference,
                    IsTest = _testMode
                };
                
                // Calculate hash signature
                var hashString = $"{_siteCode}{ozowRequest.TransactionReference}{_privateKey}";
                var hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(hashString));
                var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                // Add hash to request
                var finalRequest = new Dictionary<string, object>(
                    JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(ozowRequest)) ?? new Dictionary<string, object>())
                {
                    { "HashCheck", hash }
                };
                
                // Call Ozow API
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/post/transactionstatus", finalRequest);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ozow API error: {StatusCode}", response.StatusCode);
                    return false;
                }
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                var ozowResponse = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                
                if (ozowResponse == null || !ozowResponse.TryGetValue("Status", out var status))
                {
                    return false;
                }
                
                // Check if payment is complete
                return status.ToString()?.ToLower() == "complete";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment status for reference {PaymentReference}", paymentReference);
                return false;
            }
        }
        
        public async Task<bool> HoldPaymentInEscrowAsync(Guid orderId, decimal amount)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for escrow", orderId);
                    return false;
                }
                
                // For MVP, we're just updating the order status
                // In a production system, this would interact with a real escrow service
                
                // Calculate admin fee (10%)
                order.AdminFeeAmount = Math.Round(amount * 0.1m, 2);
                order.SellerPayoutAmount = amount - order.AdminFeeAmount;
                
                // Update transaction status
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Status == TransactionStatus.Completed);
                
                if (transaction != null)
                {
                    transaction.Status = TransactionStatus.EscrowHeld;
                }
                
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Payment of {Amount} held in escrow for order {OrderId}", amount, orderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error holding payment in escrow for order {OrderId}", orderId);
                return false;
            }
        }
        
        public async Task<bool> ReleasePaymentFromEscrowAsync(Guid orderId, decimal amount)
        {
            try
            {
                var order = await _dbContext.Orders
                    .Include(o => o.Seller)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                
                if (order == null)
                {
                    _logger.LogWarning("Order {OrderId} not found for escrow release", orderId);
                    return false;
                }
                
                // For MVP, we're just updating the order status
                // In a production system, this would initiate a bank transfer to the seller
                
                // Update order status
                order.Status = OrderStatus.AwaitingPayout;
                
                // Update transaction status
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Status == TransactionStatus.EscrowHeld);
                
                if (transaction != null)
                {
                    transaction.Status = TransactionStatus.SellerPayout;
                }
                
                // Create admin fee transaction
                var adminFeeTransaction = new Transaction
                {
                    OrderId = orderId,
                    Amount = order.AdminFeeAmount,
                    PaymentProvider = "Internal",
                    PaymentMethod = "AdminFee",
                    TransactionId = $"FEE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    Status = TransactionStatus.AdminFeePaid,
                    ProcessedAt = DateTime.UtcNow
                };
                
                _dbContext.Transactions.Add(adminFeeTransaction);
                
                // For demo purposes, immediately complete the order
                // In production, this would wait for confirmation of seller payout
                order.Status = OrderStatus.Completed;
                order.CompletedAt = DateTime.UtcNow;
                order.SellerPaidAt = DateTime.UtcNow;
                
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Payment of {Amount} released from escrow for order {OrderId}", amount, orderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing payment from escrow for order {OrderId}", orderId);
                return false;
            }
        }
    }
}
