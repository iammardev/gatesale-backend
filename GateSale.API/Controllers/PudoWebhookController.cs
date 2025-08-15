using System.IO;
using System.Text;
using GateSale.Core.Interfaces;
using GateSale.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PudoWebhookController : ControllerBase
    {
        private readonly IPudoLockerService _pudoLockerService;
        private readonly ILogger<PudoWebhookController> _logger;

        public PudoWebhookController(
            IPudoLockerService pudoLockerService,
            ILogger<PudoWebhookController> logger)
        {
            _pudoLockerService = pudoLockerService;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                // Read the request body
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                
                // Get the signature from headers
                var signature = Request.Headers["X-Pudo-Signature"].FirstOrDefault() ?? string.Empty;
                
                // Log the raw webhook
                _logger.LogInformation("Received PUDO webhook: {Body}", body);
                
                // Verify signature
                if (!await _pudoLockerService.VerifyWebhookSignature(body, signature))
                {
                    _logger.LogWarning("Invalid webhook signature");
                    // Still log the webhook but mark it as unverified
                    await _pudoLockerService.LogWebhookEvent(
                        "unknown", 
                        body, 
                        false, 
                        null, 
                        "Invalid signature");
                    
                    // Return 200 to acknowledge receipt (don't give attackers info)
                    return Ok();
                }
                
                // Parse the webhook payload
                var webhookEvent = JsonConvert.DeserializeObject<PudoWebhookEvent>(body);
                if (webhookEvent == null)
                {
                    _logger.LogWarning("Invalid webhook payload");
                    await _pudoLockerService.LogWebhookEvent(
                        "unknown", 
                        body, 
                        false, 
                        null, 
                        "Invalid payload format");
                    return Ok();
                }
                
                // Process the webhook asynchronously
                _ = Task.Run(async () => await _pudoLockerService.ProcessWebhookEvent(webhookEvent));
                
                // Return 200 immediately to acknowledge receipt
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook");
                return Ok(); // Still return 200 to acknowledge receipt
            }
        }

        [HttpPost("status")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleStatusWebhook([FromBody] LockerStatusWebhookRequest request)
        {
            try
            {
                // Convert to PudoWebhookEvent
                var webhookEvent = new PudoWebhookEvent
                {
                    EventType = PudoEventTypes.LockerStatusChange,
                    LockerCode = request.LockerCode,
                    Status = request.Status,
                    TransactionId = request.TransactionId,
                    Timestamp = DateTime.UtcNow
                };
                
                // Process the webhook
                await _pudoLockerService.ProcessWebhookEvent(webhookEvent);
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing status webhook");
                return Ok(); // Still return 200 to acknowledge receipt
            }
        }

        [HttpPost("pickup")]
        [AllowAnonymous]
        public async Task<IActionResult> HandlePickupWebhook([FromBody] PickupConfirmationWebhookRequest request)
        {
            try
            {
                // Convert to PudoWebhookEvent
                var webhookEvent = new PudoWebhookEvent
                {
                    EventType = PudoEventTypes.PackagePickedUp,
                    LockerCode = request.LockerCode,
                    OrderReference = request.OrderId.ToString(),
                    Timestamp = request.PickupTime
                };
                
                // Process the webhook
                await _pudoLockerService.ProcessWebhookEvent(webhookEvent);
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pickup webhook");
                return Ok(); // Still return 200 to acknowledge receipt
            }
        }
    }
    
    // Using the request classes defined in LockerController to avoid duplication
}
