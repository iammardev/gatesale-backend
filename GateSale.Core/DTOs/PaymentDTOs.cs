namespace GateSale.Core.DTOs
{
    // Request DTOs
    
    public class InitiatePaymentRequest
    {
        public required Guid OrderId { get; set; }
        public required string ReturnUrl { get; set; }
        public required string CancelUrl { get; set; }
    }
    
    public class PaymentWebhookRequest
    {
        public required string Payload { get; set; }
        public required string Signature { get; set; }
    }
    
    public class RefundPaymentRequest
    {
        public required Guid OrderId { get; set; }
        public required decimal Amount { get; set; }
        public required string Reason { get; set; }
    }
    
    // Response DTOs
    
    public class PaymentResultDto
    {
        public required string PaymentUrl { get; set; }
        public required string PaymentReference { get; set; }
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
    }
    
    public class WebhookResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? OrderReference { get; set; }
        public decimal? Amount { get; set; }
        public string? TransactionId { get; set; }
        public string? Status { get; set; }
    }
    
    public class RefundResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? RefundReference { get; set; }
        public decimal? Amount { get; set; }
    }
}
