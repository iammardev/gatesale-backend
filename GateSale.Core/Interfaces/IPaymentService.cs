using GateSale.Core.DTOs;

namespace GateSale.Core.Interfaces
{
    public interface IPaymentService
    {
        // Payment Initiation
        Task<PaymentResultDto> InitiatePaymentAsync(Guid orderId, decimal amount, string returnUrl, string cancelUrl);
        
        // Webhook Handling
        Task<bool> ValidateWebhookAsync(string payload, string signature);
        Task<WebhookResultDto> ProcessPaymentWebhookAsync(string payload);
        
        // Refund Processing
        Task<RefundResultDto> ProcessRefundAsync(Guid orderId, decimal amount, string reason);
        
        // Payment Status
        Task<bool> VerifyPaymentStatusAsync(string paymentReference);
        
        // Escrow Management
        Task<bool> HoldPaymentInEscrowAsync(Guid orderId, decimal amount);
        Task<bool> ReleasePaymentFromEscrowAsync(Guid orderId, decimal amount);
    }
}
