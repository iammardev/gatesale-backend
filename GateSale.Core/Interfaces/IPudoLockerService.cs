using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Models;

namespace GateSale.Core.Interfaces
{
    public interface IPudoLockerService
    {
        // Locker management
        Task<IEnumerable<Locker>> GetAvailableLockers(double latitude, double longitude, double radiusInKm);
        Task<Locker> GetLockerByCode(string lockerCode);
        Task<bool> ReserveLocker(string lockerCode, Guid orderId);
        
        // Order-locker operations
        Task<bool> AssignOrderToLocker(Guid orderId, string lockerCode);
        Task<string> GenerateAccessCode(Guid orderId, string lockerCode);
        Task<bool> ReleaseLocker(string lockerCode, string accessCode);
        
        // Webhook handling for status updates
        Task ProcessLockerStatusUpdate(string lockerCode, LockerStatus newStatus, string transactionId);
        Task ProcessOrderPickupConfirmation(Guid orderId, string lockerCode, DateTime pickupTime);
        
        // Enhanced webhook handling
        Task ProcessWebhookEvent(PudoWebhookEvent webhookEvent);
        Task<bool> VerifyWebhookSignature(string payload, string signature);
        Task LogWebhookEvent(string eventType, string rawPayload, bool isProcessed = false, string? result = null, string? errorMessage = null);
    }
} 