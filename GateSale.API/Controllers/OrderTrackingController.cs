using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderTrackingController : ControllerBase
    {
        private readonly IOrderTrackingService _orderTrackingService;
        private readonly ILogger<OrderTrackingController> _logger;

        public OrderTrackingController(
            IOrderTrackingService orderTrackingService,
            ILogger<OrderTrackingController> logger)
        {
            _orderTrackingService = orderTrackingService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token");
            }
            return userId;
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderTracking(Guid orderId)
        {
            try
            {
                var trackingInfo = await _orderTrackingService.GetOrderTrackingInfo(orderId);
                return Ok(trackingInfo);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order tracking for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while retrieving order tracking information");
            }
        }

        [HttpGet("{orderId}/locker-status")]
        public async Task<IActionResult> GetOrderLockerStatus(Guid orderId)
        {
            try
            {
                var lockerInfo = await _orderTrackingService.GetOrderLockerStatus(orderId);
                
                if (lockerInfo == null)
                {
                    return NotFound($"No locker assigned to order {orderId}");
                }
                
                return Ok(lockerInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locker status for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while retrieving locker status");
            }
        }

        [HttpPost("{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                var result = await _orderTrackingService.UpdateOrderStatus(orderId, request.Status, request.Notes);
                
                if (!result)
                {
                    return NotFound($"Order {orderId} not found");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while updating order status");
            }
        }

        [HttpPost("{orderId}/events")]
        public async Task<IActionResult> LogOrderEvent(Guid orderId, [FromBody] LogOrderEventRequest request)
        {
            try
            {
                var result = await _orderTrackingService.LogOrderTrackingEvent(
                    orderId, 
                    request.EventType, 
                    request.Description, 
                    request.Location);
                
                if (!result)
                {
                    return NotFound($"Order {orderId} not found");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging event for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while logging order event");
            }
        }
    }

    public class UpdateOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    public class LogOrderEventRequest
    {
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Location { get; set; }
    }
}
