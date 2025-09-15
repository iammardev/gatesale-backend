namespace GateSale.Core.Enums
{
    public enum OrderStatus
    {
        // Purchase Flow Statuses
        PaidAwaitingShipment = 0,
        InTransit = 1,
        Delivered = 2,
        Collected = 3,
        BuyerApproved = 4,
        
        // Dispute Flow Statuses
        DisputeInProgress = 10,
        DisputeApproved = 11,
        DisputeRejected = 12,
        
        // Return Flow Statuses
        AwaitingReturn = 20,
        ReturnInTransit = 21,
        ReturnDelivered = 22,
        ReturnCollected = 23,
        
        // Cancellation Statuses
        CancelledBySeller = 30,
        
        // Payment Flow Statuses
        AwaitingRefund = 40,
        Refunded = 41,
        AwaitingPayout = 42,
        
        // Final Status
        Completed = 50
    }
    
    public enum TransactionStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3,
        Refunded = 4,
        Cancelled = 5,
        EscrowHeld = 6,
        PartialRefund = 7,
        SellerPayout = 8,
        AdminFeePaid = 9
    }
    
    public enum DisputeStatus
    {
        Open = 0,
        InReview = 1,
        Approved = 2,
        Rejected = 3,
        AwaitingReturn = 4,
        ReturnReceived = 5,
        Resolved = 6,
        Closed = 7
    }
    
    public enum DisputeReason
    {
        ItemNotAsDescribed = 0,
        ItemDamaged = 1,
        ItemNotWorking = 2,
        WrongItemReceived = 3,
        PoorCondition = 4,
        Other = 5
    }
}