using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }
        
        public decimal Amount { get; set; }
        
        public string PaymentProvider { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        
        public TransactionStatus Status { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        
        public string? Notes { get; set; }
    }
}