using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LockerController : ControllerBase
    {
        private readonly IPudoLockerService _pudoLockerService;
        private readonly ILogger<LockerController> _logger;

        public LockerController(
            IPudoLockerService pudoLockerService,
            ILogger<LockerController> logger)
        {
            _pudoLockerService = pudoLockerService;
            _logger = logger;
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearbyLockers(
            [FromQuery] double latitude, 
            [FromQuery] double longitude, 
            [FromQuery] double radius = 10)
        {
            try
            {
                var lockers = await _pudoLockerService.GetAvailableLockers(latitude, longitude, radius);
                return Ok(lockers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby lockers");
                
                // Return more detailed error information for debugging
                return StatusCode(500, new { 
                    Message = "An error occurred while retrieving lockers",
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    // For testing only - remove in production:
                    StackTrace = ex.StackTrace 
                });
            }
        }

        [HttpGet("{lockerCode}")]
        public async Task<IActionResult> GetLocker(string lockerCode)
        {
            try
            {
                var locker = await _pudoLockerService.GetLockerByCode(lockerCode);
                return Ok(locker);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting locker {LockerCode}", lockerCode);
                return StatusCode(500, $"An error occurred while retrieving locker {lockerCode}");
            }
        }

        [Authorize]
        [HttpPost("assign/{orderId}")]
        public async Task<IActionResult> AssignOrderToLocker(Guid orderId, [FromBody] AssignLockerRequest request)
        {
            try
            {
                var result = await _pudoLockerService.AssignOrderToLocker(orderId, request.LockerCode);
                
                if (!result)
                {
                    return BadRequest("Failed to assign order to locker");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning order {OrderId} to locker", orderId);
                return StatusCode(500, "An error occurred while assigning the order to a locker");
            }
        }

        [Authorize]
        [HttpPost("access-code/{orderId}")]
        public async Task<IActionResult> GenerateAccessCode(Guid orderId, [FromBody] GenerateAccessCodeRequest request)
        {
            try
            {
                var accessCode = await _pudoLockerService.GenerateAccessCode(orderId, request.LockerCode);
                return Ok(new { AccessCode = accessCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access code for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while generating the access code");
            }
        }

        [Authorize]
        [HttpPost("release")]
        public async Task<IActionResult> ReleaseLocker([FromBody] ReleaseLockerRequest request)
        {
            try
            {
                var result = await _pudoLockerService.ReleaseLocker(request.LockerCode, request.AccessCode);
                
                if (!result)
                {
                    return BadRequest("Failed to release locker");
                }
                
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing locker {LockerCode}", request.LockerCode);
                return StatusCode(500, "An error occurred while releasing the locker");
            }
        }
        
        [HttpPost("webhook/status")]
        [AllowAnonymous]
        public async Task<IActionResult> LockerStatusWebhook([FromBody] LockerStatusWebhookRequest request)
        {
            try
            {
                await _pudoLockerService.ProcessLockerStatusUpdate(
                    request.LockerCode, 
                    Enum.Parse<LockerStatus>(request.Status, true), 
                    request.TransactionId);
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing locker status webhook");
                // Return OK anyway to acknowledge receipt
                return Ok();
            }
        }
        
        [HttpPost("webhook/pickup")]
        [AllowAnonymous]
        public async Task<IActionResult> PickupConfirmationWebhook([FromBody] PickupConfirmationWebhookRequest request)
        {
            try
            {
                await _pudoLockerService.ProcessOrderPickupConfirmation(
                    request.OrderId, 
                    request.LockerCode, 
                    request.PickupTime);
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pickup confirmation webhook");
                // Return OK anyway to acknowledge receipt
                return Ok();
            }
        }
    }

    public class AssignLockerRequest
    {
        public required string LockerCode { get; set; }
    }

    public class GenerateAccessCodeRequest
    {
        public required string LockerCode { get; set; }
    }

    public class ReleaseLockerRequest
    {
        public required string LockerCode { get; set; }
        public required string AccessCode { get; set; }
    }
    
    public class LockerStatusWebhookRequest
    {
        public required string LockerCode { get; set; }
        public required string Status { get; set; }
        public required string TransactionId { get; set; }
    }
    
    public class PickupConfirmationWebhookRequest
    {
        public required Guid OrderId { get; set; }
        public required string LockerCode { get; set; }
        public DateTime PickupTime { get; set; } = DateTime.UtcNow;
    }
} 