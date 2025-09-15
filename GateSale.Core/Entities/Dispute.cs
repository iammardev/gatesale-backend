using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Dispute
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        // Dispute Details
        public DisputeReason ReasonCode { get; set; }
        public required string Reason { get; set; }
        public string? Description { get; set; }
        public DisputeStatus Status { get; set; } = DisputeStatus.Open;
        
        // Evidence
        public ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
        
        // Admin Review
        public string? AdminNotes { get; set; }
        public Guid? ReviewedById { get; set; }
        public User? ReviewedBy { get; set; }
        public bool? IsApproved { get; set; }
        public string? RejectionReason { get; set; }
        
        // Return Details
        public bool IsReturnRequested { get; set; }
        public bool IsReturnPaidBySeller { get; set; }
        public bool IsReturnCompleted { get; set; }
        
        // Refund Details
        public bool IsRefundIssued { get; set; }
        public decimal? RefundAmount { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
    
    public class DisputeEvidence
    {
        public Guid Id { get; set; }
        public Guid DisputeId { get; set; }
        public Dispute Dispute { get; set; } = null!;
        
        public required string FileUrl { get; set; }
        public string? Caption { get; set; }
        public string? FileType { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}