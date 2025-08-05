using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public decimal Amount { get; set; }
        public required string PaymentProvider { get; set; }
        public required string PaymentMethod { get; set; }
        public required string TransactionId { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
    }
}