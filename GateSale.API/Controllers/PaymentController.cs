using GateSale.Core.DTOs;
using GateSale.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IOrderService orderService,
            GateSaleDbContext dbContext,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _orderService = orderService;
            _dbContext = dbContext;
            _logger = logger;
        }

        private async Task<Guid> GetCurrentUserId()
        {
            // Get Cognito user ID from token
            var cognitoUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cognitoUserIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            
            _logger.LogInformation("Found Cognito User ID in token: {CognitoUserId}", cognitoUserIdClaim);
            
            // Look up user by Cognito ID
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.CognitoUserId == cognitoUserIdClaim);
            
            if (user == null)
            {
                _logger.LogWarning("No user found with Cognito ID: {CognitoUserId}", cognitoUserIdClaim);
                throw new UnauthorizedAccessException("User not found in database");
            }
            
            _logger.LogInformation("Found user in database. User ID: {UserId}, Username: {Username}", user.Id, user.Username);
            return user.Id;
        }

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Verify that the order exists and belongs to the user
                var order = await _orderService.GetOrderByIdAsync(request.OrderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {request.OrderId} not found");
                }
                
                if (order.BuyerId != userId)
                {
                    return Forbid("You are not authorized to initiate payment for this order");
                }
                
                // Initiate payment
                var paymentResult = await _paymentService.InitiatePaymentAsync(
                    order.Id,
                    order.TotalAmount,
                    request.ReturnUrl,
                    request.CancelUrl);
                
                if (paymentResult == null)
                {
                    return StatusCode(500, "Failed to initiate payment");
                }
                
                return Ok(new
                {
                    PaymentUrl = paymentResult.PaymentUrl,
                    PaymentReference = paymentResult.PaymentReference
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating payment for order {OrderId}", request.OrderId);
                return StatusCode(500, "An error occurred while initiating payment");
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous] // Allow anonymous access for payment gateway webhooks
        public async Task<IActionResult> PaymentWebhook([FromBody] PaymentWebhookRequest request)
        {
            try
            {
                // Validate webhook signature
                var isValid = await _paymentService.ValidateWebhookAsync(request.Payload, request.Signature);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid payment webhook signature");
                    return BadRequest("Invalid signature");
                }
                
                // Process the payment notification
                var result = await _paymentService.ProcessPaymentWebhookAsync(request.Payload);
                
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to process payment webhook: {Message}", result.Message);
                    // Still return 200 to acknowledge receipt
                    return Ok(new { Success = false, Message = result.Message });
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment webhook");
                // Still return 200 to acknowledge receipt
                return Ok(new { Success = false, Message = "Internal server error" });
            }
        }

        [HttpPost("refund")]
        [Authorize(Roles = "Admin")] // Only admins can initiate refunds
        public async Task<IActionResult> RefundPayment([FromBody] RefundPaymentRequest request)
        {
            try
            {
                // Verify that the order exists
                var order = await _orderService.GetOrderByIdAsync(request.OrderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {request.OrderId} not found");
                }
                
                // Process refund
                var refundResult = await _paymentService.ProcessRefundAsync(
                    order.Id,
                    request.Amount,
                    request.Reason);
                
                if (!refundResult.Success)
                {
                    return StatusCode(500, $"Failed to process refund: {refundResult.Message}");
                }
                
                return Ok(new
                {
                    Success = true,
                    RefundReference = refundResult.RefundReference
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for order {OrderId}", request.OrderId);
                return StatusCode(500, "An error occurred while processing refund");
            }
        }
    }
}
