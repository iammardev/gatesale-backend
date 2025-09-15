using GateSale.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace GateSale.Core.DTOs
{
    // Request DTOs
    
    public class CreateDisputeRequest
    {
        public DisputeReason ReasonCode { get; set; }
        public required string Reason { get; set; }
        public string? Description { get; set; }
    }
    
    public class UploadDisputeEvidenceRequest
    {
        public required IFormFile File { get; set; }
        public string? Caption { get; set; }
    }
    
    public class ReviewDisputeRequest
    {
        public required bool IsApproved { get; set; }
        public string? Notes { get; set; }
    }
    
    public class RequestReturnRequest
    {
        public required bool IsSellerPayingForReturn { get; set; }
    }
    
    public class MarkReturnShippedRequest
    {
        public required string ReturnTrackingNumber { get; set; }
        public string? ReturnShipmentReference { get; set; }
    }
    
    // Response DTOs
    
    public class DisputeDetailDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public DisputeReason ReasonCode { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DisputeStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public bool? IsApproved { get; set; }
        public string? AdminNotes { get; set; }
        public bool IsReturnRequested { get; set; }
        public bool IsReturnPaidBySeller { get; set; }
        public bool IsReturnCompleted { get; set; }
        public List<DisputeEvidenceDto> Evidence { get; set; } = new List<DisputeEvidenceDto>();
    }
    
    public class DisputeEvidenceDto
    {
        public Guid Id { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public string? FileType { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
