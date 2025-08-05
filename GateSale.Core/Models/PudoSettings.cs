namespace GateSale.Core.Models
{
    public class PudoSettings
    {
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public bool UseSandbox { get; set; } = false;
    }
} 