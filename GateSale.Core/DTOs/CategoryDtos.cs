namespace GateSale.Core.DTOs
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public List<SubCategoryDto> SubCategories { get; set; } = new();
    }

    public class SubCategoryDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public Guid CategoryId { get; set; }
    }
}
