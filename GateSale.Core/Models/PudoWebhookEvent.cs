namespace GateSale.Core.Models
{
    public class PudoWebhookEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string LockerCode { get; set; } = string.Empty;
        public string? OrderReference { get; set; }
        public string? TransactionId { get; set; }
        public string? Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? AccessCode { get; set; }
        public string? RecipientPhone { get; set; }
        public string? RecipientEmail { get; set; }
        public string? Signature { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();
    }

    public static class PudoEventTypes
    {
        public const string LockerStatusChange = "locker_status_change";
        public const string PackageDropped = "package_dropped";
        public const string PackagePickedUp = "package_picked_up";
        public const string AccessCodeGenerated = "access_code_generated";
        public const string AccessCodeUsed = "access_code_used";
        public const string LockerReserved = "locker_reserved";
        public const string LockerReleased = "locker_released";
        public const string AccessDenied = "access_denied";
    }
}
