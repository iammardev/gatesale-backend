using GateSale.Core.DTOs;
using GateSale.Core.Entities;
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
    public class DisputeController : ControllerBase
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<DisputeController> _logger;

        public DisputeController(
            GateSaleDbContext dbContext,
            IOrderService orderService,
            IPaymentService paymentService,
            ILogger<DisputeController> logger)
        {
            _dbContext = dbContext;
            _orderService = orderService;
            _paymentService = paymentService;
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

        [HttpPost("order/{orderId}")]
        public async Task<IActionResult> CreateDispute(Guid orderId, [FromBody] CreateDisputeRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the user is the buyer
                if (order.BuyerId != userId)
                {
                    return Forbid("Only the buyer can create a dispute for this order");
                }
                
                // Verify that the order is in a valid state for dispute
                if (order.Status != OrderStatus.Collected)
                {
                    return BadRequest($"Cannot create dispute for order in status {order.Status}. Order must be in Collected status.");
                }
                
                // Check if a dispute already exists
                if (order.Dispute != null)
                {
                    return BadRequest("A dispute already exists for this order");
                }
                
                // Create dispute
                var dispute = new Dispute
                {
                    OrderId = orderId,
                    ReasonCode = request.ReasonCode,
                    Reason = request.Reason,
                    Description = request.Description,
                    Status = DisputeStatus.Open,
                    CreatedAt = DateTime.UtcNow
                };
                
                _dbContext.Disputes.Add(dispute);
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.DisputeInProgress, 
                    $"Dispute initiated: {request.Reason}");
                
                order.DisputeInitiatedAt = DateTime.UtcNow;
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    DisputeId = dispute.Id,
                    Status = dispute.Status.ToString(),
                    CreatedAt = dispute.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating dispute for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while creating the dispute");
            }
        }

        [HttpPost("{disputeId}/evidence")]
        public async Task<IActionResult> UploadDisputeEvidence(Guid disputeId, [FromForm] UploadDisputeEvidenceRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the dispute
                var dispute = await _dbContext.Disputes
                    .Include(d => d.Order)
                    .FirstOrDefaultAsync(d => d.Id == disputeId);
                
                if (dispute == null)
                {
                    return NotFound($"Dispute with ID {disputeId} not found");
                }
                
                // Verify that the user is the buyer
                if (dispute.Order.BuyerId != userId)
                {
                    return Forbid("Only the buyer can upload evidence for this dispute");
                }
                
                // Process file upload
                if (request.File != null && request.File.Length > 0)
                {
                    // In a real implementation, you would upload to S3 or another storage service
                    // For this example, we'll just store the file name
                    
                    var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
                    var fileUrl = $"https://storage.example.com/dispute-evidence/{fileName}";
                    
                    // Create evidence record
                    var evidence = new DisputeEvidence
                    {
                        DisputeId = disputeId,
                        FileUrl = fileUrl,
                        Caption = request.Caption,
                        FileType = request.File.ContentType,
                        UploadedAt = DateTime.UtcNow
                    };
                    
                    _dbContext.DisputeEvidence.Add(evidence);
                    await _dbContext.SaveChangesAsync();
                    
                    return Ok(new
                    {
                        EvidenceId = evidence.Id,
                        FileUrl = evidence.FileUrl,
                        Caption = evidence.Caption,
                        UploadedAt = evidence.UploadedAt
                    });
                }
                
                return BadRequest("No file uploaded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading evidence for dispute {DisputeId}", disputeId);
                return StatusCode(500, "An error occurred while uploading evidence");
            }
        }

        [HttpPost("{disputeId}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReviewDispute(Guid disputeId, [FromBody] ReviewDisputeRequest request)
        {
            try
            {
                var adminId = await GetCurrentUserId();
                
                // Get the dispute
                var dispute = await _dbContext.Disputes
                    .Include(d => d.Order)
                    .FirstOrDefaultAsync(d => d.Id == disputeId);
                
                if (dispute == null)
                {
                    return NotFound($"Dispute with ID {disputeId} not found");
                }
                
                // Update dispute status
                dispute.Status = request.IsApproved ? DisputeStatus.Approved : DisputeStatus.Rejected;
                dispute.AdminNotes = request.Notes;
                dispute.ReviewedById = adminId;
                dispute.ReviewedAt = DateTime.UtcNow;
                dispute.IsApproved = request.IsApproved;
                
                if (!request.IsApproved)
                {
                    dispute.RejectionReason = request.Notes;
                }
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    dispute.OrderId, 
                    request.IsApproved ? OrderStatus.DisputeApproved : OrderStatus.DisputeRejected, 
                    $"Dispute {(request.IsApproved ? "approved" : "rejected")}: {request.Notes}");
                
                dispute.Order.DisputeResolvedAt = DateTime.UtcNow;
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    DisputeId = dispute.Id,
                    Status = dispute.Status.ToString(),
                    IsApproved = dispute.IsApproved,
                    ReviewedAt = dispute.ReviewedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing dispute {DisputeId}", disputeId);
                return StatusCode(500, "An error occurred while reviewing the dispute");
            }
        }

        [HttpPost("order/{orderId}/return")]
        public async Task<IActionResult> RequestReturn(Guid orderId, [FromBody] RequestReturnRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the user is the seller
                if (order.SellerId != userId)
                {
                    return Forbid("Only the seller can request a return for this order");
                }
                
                // Verify that the order is in a valid state for return
                if (order.Status != OrderStatus.DisputeApproved)
                {
                    return BadRequest($"Cannot request return for order in status {order.Status}. Order must be in DisputeApproved status.");
                }
                
                // Update dispute
                if (order.Dispute == null)
                {
                    return BadRequest("No dispute found for this order");
                }
                
                order.Dispute.IsReturnRequested = true;
                order.Dispute.IsReturnPaidBySeller = request.IsSellerPayingForReturn;
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.AwaitingReturn, 
                    $"Return requested by seller. Seller paying for return: {request.IsSellerPayingForReturn}");
                
                order.ReturnInitiatedAt = DateTime.UtcNow;
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    OrderId = order.Id,
                    Status = order.Status.ToString(),
                    IsReturnRequested = order.Dispute.IsReturnRequested,
                    IsSellerPayingForReturn = order.Dispute.IsReturnPaidBySeller
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting return for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while requesting the return");
            }
        }

        [HttpPost("order/{orderId}/return/shipped")]
        public async Task<IActionResult> MarkReturnShipped(Guid orderId, [FromBody] MarkReturnShippedRequest request)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the user is the buyer
                if (order.BuyerId != userId)
                {
                    return Forbid("Only the buyer can mark a return as shipped");
                }
                
                // Verify that the order is in a valid state
                if (order.Status != OrderStatus.AwaitingReturn)
                {
                    return BadRequest($"Cannot mark return as shipped for order in status {order.Status}. Order must be in AwaitingReturn status.");
                }
                
                // Update return shipping details
                order.ReturnTrackingNumber = request.ReturnTrackingNumber;
                order.ReturnShipmentReference = request.ReturnShipmentReference;
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.ReturnInTransit, 
                    $"Return shipped with tracking number {request.ReturnTrackingNumber}");
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    OrderId = order.Id,
                    Status = order.Status.ToString(),
                    ReturnTrackingNumber = order.ReturnTrackingNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking return as shipped for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while marking the return as shipped");
            }
        }

        [HttpPost("order/{orderId}/return/delivered")]
        public async Task<IActionResult> MarkReturnDelivered(Guid orderId)
        {
            try
            {
                // This endpoint would typically be called by a webhook from the shipping provider
                // For this example, we'll allow it to be called directly
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the order is in a valid state
                if (order.Status != OrderStatus.ReturnInTransit)
                {
                    return BadRequest($"Cannot mark return as delivered for order in status {order.Status}. Order must be in ReturnInTransit status.");
                }
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.ReturnDelivered, 
                    "Return delivered to seller's Pudo locker");
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    OrderId = order.Id,
                    Status = order.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking return as delivered for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while marking the return as delivered");
            }
        }

        [HttpPost("order/{orderId}/return/collected")]
        public async Task<IActionResult> MarkReturnCollected(Guid orderId)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the user is the seller
                if (order.SellerId != userId)
                {
                    return Forbid("Only the seller can mark a return as collected");
                }
                
                // Verify that the order is in a valid state
                if (order.Status != OrderStatus.ReturnDelivered)
                {
                    return BadRequest($"Cannot mark return as collected for order in status {order.Status}. Order must be in ReturnDelivered status.");
                }
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.ReturnCollected, 
                    "Return collected by seller");
                
                order.ReturnCompletedAt = DateTime.UtcNow;
                
                // Update dispute
                if (order.Dispute != null)
                {
                    order.Dispute.IsReturnCompleted = true;
                }
                
                await _dbContext.SaveChangesAsync();
                
                // Process refund
                if (order.Transaction != null)
                {
                    await _paymentService.ProcessRefundAsync(
                        orderId,
                        order.TotalAmount,
                        "Return completed, refund issued");
                }
                
                return Ok(new
                {
                    OrderId = order.Id,
                    Status = order.Status.ToString(),
                    ReturnCompletedAt = order.ReturnCompletedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking return as collected for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while marking the return as collected");
            }
        }

        [HttpPost("order/{orderId}/waive-return")]
        public async Task<IActionResult> WaiveReturn(Guid orderId)
        {
            try
            {
                var userId = await GetCurrentUserId();
                
                // Get the order
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return NotFound($"Order with ID {orderId} not found");
                }
                
                // Verify that the user is the seller
                if (order.SellerId != userId)
                {
                    return Forbid("Only the seller can waive a return for this order");
                }
                
                // Verify that the order is in a valid state
                if (order.Status != OrderStatus.DisputeApproved)
                {
                    return BadRequest($"Cannot waive return for order in status {order.Status}. Order must be in DisputeApproved status.");
                }
                
                // Update order status
                await _orderService.UpdateOrderStatusAsync(
                    orderId, 
                    OrderStatus.AwaitingRefund, 
                    "Return waived by seller, processing refund");
                
                // Process refund
                if (order.Transaction != null)
                {
                    await _paymentService.ProcessRefundAsync(
                        orderId,
                        order.TotalAmount,
                        "Return waived by seller, refund issued");
                }
                
                await _dbContext.SaveChangesAsync();
                
                return Ok(new
                {
                    OrderId = order.Id,
                    Status = order.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiving return for order {OrderId}", orderId);
                return StatusCode(500, "An error occurred while waiving the return");
            }
        }
    }
}
