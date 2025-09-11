namespace GateSale.Core.Entities
{
    public class Category
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }

        // Navigation
        public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
    }

    public class SubCategory
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }

        // Foreign key
        public Guid CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}
