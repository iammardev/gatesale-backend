namespace GateSale.Core.Enums
{
    public enum ProductStatus
    {
        // Listing Status
        PendingApproval = 0,
        Listed = 1,
        FlaggedForReview = 2,
        Rejected = 3,
        ListingExpired = 4,
        
        // Sales Status
        Available = 10,
        Reserved = 11,
        Sold = 12,
        
        // Administrative Status
        Inactive = 20,
        Deleted = 21
    }
    
    public enum ProductCondition
    {
        New = 0,
        LikeNew = 1,
        Good = 2,
        Fair = 3,
        Poor = 4
    }
}