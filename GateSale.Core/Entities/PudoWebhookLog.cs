namespace GateSale.Core.Entities
{
    public class PudoWebhookLog
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string RawPayload { get; set; } = string.Empty;
        public bool IsProcessed { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessingResult { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
