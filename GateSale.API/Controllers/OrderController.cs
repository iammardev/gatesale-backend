using GateSale.Core.DTOs;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IOrderService orderService,
            GateSaleDbContext dbContext,
            ILogger<OrderController> logger)
        {
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

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var buyerId = await GetCurrentUserId();
                
                // Log the buyer ID for debugging
                _logger.LogInformation("Attempting to create order with buyer ID: {BuyerId}", buyerId);
                
                // Check if user exists in database
                var userExists = await _orderService.CheckUserExistsAsync(buyerId);
                if (!userExists)
                {
                    _logger.LogWarning("User with ID {BuyerId} does not exist in the database", buyerId);
                    return BadRequest("User not found. The user ID from your token does not match any user in our system.");
                }
                
                // Validate the order
                var isValid = await _orderService.ValidateOrderForPaymentAsync(request.ProductId, buyerId);
                if (!isValid)
                {
                    return BadRequest("Invalid order request. The product may not be available or you may not have permission to purchase it.");
                }
                
                // Create the order
                var order = await _orderService.CreateOrderAsync(
                    request.ProductId,
                    buyerId,
                    request.BuyerLockerId,
                    request.ShippingCost);
                
                if (order == null)
                {
                    return BadRequest("Failed to create order");
                }
                
                // Return order summary
                return Ok(new OrderSummaryDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    TotalAmount = order.TotalAmount,
                    ShippingCost = order.ShippingCost
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, "An error occurred while creating the order");
            }
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrder(Guid orderId)
        {
            try
            {
                var userId = await GetCurrentUserId();
                var order = await _orderService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Check if the user is the buyer or seller of this order
                if (order.BuyerId != userId && order.SellerId != userId)
                {
                    return Forbid();
                }
                
                // Map order to OrderDetailDto
                var orderDetail = new OrderDetailDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    OrderDate = order.OrderDate,
                    Status = order.Status,
                    StatusLabel = order.Status.ToString(),
                    
                    // Financial Details
                    ItemSubtotal = order.ItemSubtotal,
                    ShippingCost = order.ShippingCost,
                    TotalAmount = order.TotalAmount,
                    AdminFeeAmount = order.AdminFeeAmount,
                    SellerPayoutAmount = order.SellerPayoutAmount,
                    
                    // Product Details from first item (assuming single item orders for now)
                    ProductId = order.Items.FirstOrDefault()?.ProductId ?? Guid.Empty,
                    ProductTitle = order.Items.FirstOrDefault()?.Product?.Title ?? "Unknown Product",
                    ProductDescription = order.Items.FirstOrDefault()?.Product?.Description,
                    ProductImageUrl = order.Items.FirstOrDefault()?.Product?.Images.FirstOrDefault()?.ImageUrl,
                    Category = order.Items.FirstOrDefault()?.Product?.Category ?? "Unknown",
                    SubCategory = order.Items.FirstOrDefault()?.Product?.SubCategory,
                    
                    // Shipping Details
                    PudoTrackingNumber = order.PudoTrackingNumber,
                    PudoShipmentReference = order.PudoShipmentReference,
                    PackageSize = order.PackageSize,
                    
                    // Timestamps
                    ShippedAt = order.ShippedAt,
                    DeliveredAt = order.DeliveredAt,
                    CollectedAt = order.CollectedAt,
                    ApprovedAt = order.ApprovedAt,
                    CompletedAt = order.CompletedAt,
                    CancelledAt = order.CancelledAt,
                    
                    // Dispute Info
                    HasDispute = order.Dispute != null
                };
                
                // Map locker details if available
                if (order.BuyerLocker != null)
                {
                    orderDetail.BuyerLocker = new LockerDto
                    {
                        Id = order.BuyerLocker.Id,
                        LockerCode = order.BuyerLocker.LockerCode,
                        Location = order.BuyerLocker.Location,
                        Description = order.BuyerLocker.Description,
                        Latitude = order.BuyerLocker.Latitude,
                        Longitude = order.BuyerLocker.Longitude
                    };
                }
                
                if (order.SellerLocker != null)
                {
                    orderDetail.SellerLocker = new LockerDto
                    {
                        Id = order.SellerLocker.Id,
                        LockerCode = order.SellerLocker.LockerCode,
                        Location = order.SellerLocker.Location,
                        Description = order.SellerLocker.Description,
                        Latitude = order.SellerLocker.Latitude,
                        Longitude = order.SellerLocker.Longitude
                    };
                }
                
                // Map dispute details if available
                if (order.Dispute != null)
                {
                    orderDetail.Dispute = new DisputeDto
                    {
                        Id = order.Dispute.Id,
                        ReasonCode = order.Dispute.ReasonCode,
                        Reason = order.Dispute.Reason,
                        Description = order.Dispute.Description,
                        Status = order.Dispute.Status,
                        IsReturnRequested = order.Dispute.IsReturnRequested,
                        IsReturnPaidBySeller = order.Dispute.IsReturnPaidBySeller,
                        CreatedAt = order.Dispute.CreatedAt
                    };
                }
                
                return Ok(orderDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while retrieving the order");
            }
        }

        [HttpGet("buyer")]
        public async Task<IActionResult> GetBuyerOrders()
        {
            try
            {
                var buyerId = await GetCurrentUserId();
                var orders = await _orderService.GetOrdersByBuyerIdAsync(buyerId);
                
                // Map orders to OrderSummaryDto list
                var orderSummaries = orders.Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    ShippingCost = o.ShippingCost,
                    ProductTitle = o.Items.FirstOrDefault()?.Product?.Title,
                    ProductImageUrl = o.Items.FirstOrDefault()?.Product?.Images.FirstOrDefault()?.ImageUrl,
                    BuyerLockerId = o.BuyerLockerId,
                    SellerLockerId = o.SellerLockerId
                }).ToList();
                
                return Ok(orderSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving buyer orders");
                return StatusCode(500, "An error occurred while retrieving buyer orders");
            }
        }

        [HttpGet("seller")]
        public async Task<IActionResult> GetSellerOrders()
        {
            try
            {
                var sellerId = await GetCurrentUserId();
                var orders = await _orderService.GetOrdersBySellerIdAsync(sellerId);
                
                // Map orders to OrderSummaryDto list
                var orderSummaries = orders.Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    OrderDate = o.OrderDate,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    ShippingCost = o.ShippingCost,
                    ProductTitle = o.Items.FirstOrDefault()?.Product?.Title,
                    ProductImageUrl = o.Items.FirstOrDefault()?.Product?.Images.FirstOrDefault()?.ImageUrl,
                    BuyerLockerId = o.BuyerLockerId,
                    SellerLockerId = o.SellerLockerId
                }).ToList();
                
                return Ok(orderSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seller orders");
                return StatusCode(500, "An error occurred while retrieving seller orders");
            }
        }

        [HttpPost("{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                var order = await _orderService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Check if the user has permission to update this order's status
                if (order.BuyerId != userId && order.SellerId != userId)
                {
                    return Forbid();
                }
                
                // Validate status transition based on user role and current status
                // TODO: Implement proper validation logic
                
                var result = await _orderService.UpdateOrderStatusAsync(orderId, request.Status, request.Notes);
                
                if (!result)
                {
                    return BadRequest("Failed to update order status");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while updating order status");
            }
        }

        [HttpPost("{orderId}/shipment")]
        public async Task<IActionResult> ProcessShipment(Guid orderId, [FromBody] ProcessShipmentRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                var order = await _orderService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the current user is the seller
                if (order.SellerId != userId)
                {
                    return Forbid();
                }
                
                // Validate that the order is in the correct state for shipment
                var isValid = await _orderService.ValidateOrderForShipmentAsync(orderId);
                if (!isValid)
                {
                    return BadRequest("Order is not in a valid state for shipment");
                }
                
                var result = await _orderService.ProcessSellerShipmentAsync(
                    orderId, 
                    request.PudoTrackingNumber,
                    request.PudoShipmentReference);
                
                if (!result)
                {
                    return BadRequest("Failed to process shipment");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing shipment for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while processing shipment");
            }
        }

        [HttpPost("{orderId}/approve")]
        public async Task<IActionResult> ApproveOrder(Guid orderId)
        {
            try
            {
                var userId = await GetCurrentUserId();
                var order = await _orderService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the current user is the buyer
                if (order.BuyerId != userId)
                {
                    return Forbid();
                }
                
                var result = await _orderService.ApproveOrderByBuyerAsync(orderId);
                
                if (!result)
                {
                    return BadRequest("Failed to approve order");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while approving the order");
            }
        }

        [HttpPost("{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                var order = await _orderService.GetOrderByIdAsync(orderId);
                
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the current user is the seller
                if (order.SellerId != userId)
                {
                    return Forbid();
                }
                
                var result = await _orderService.CancelOrderBySellerAsync(orderId, request.Reason, request.Description);
                
                if (!result)
                {
                    return BadRequest("Failed to cancel order");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while cancelling the order");
            }
        }
    }
}
