namespace GateSale.Core.Enums
{
    public enum OrderStatus
    {
        Pending = 0,
        Confirmed = 1,
        InTransit = 2,
        ReadyForPickup = 3,
        Completed = 4,
        Cancelled = 5,
        Disputed = 6
    }
    
    public enum TransactionStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4,
        Cancelled = 5
    }
    
    public enum DisputeStatus
    {
        Open = 0,
        InReview = 1,
        Resolved = 2,
        Closed = 3
    }
}