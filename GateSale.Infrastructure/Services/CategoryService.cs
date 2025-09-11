using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GateSale.Infrastructure.Services
{
    public class CategoryService
    {
        private readonly GateSaleDbContext _context;

        public CategoryService(GateSaleDbContext context)
        {
            _context = context;
        }

        // Get all categories with subcategories
        public async Task<List<CategoryDto>> GetCategoriesAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .ToListAsync();

            return categories.Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                SubCategories = c.SubCategories.Select(sc => new SubCategoryDto
                {
                    Id = sc.Id,
                    Name = sc.Name,
                    MinPrice = sc.MinPrice,
                    MaxPrice = sc.MaxPrice,
                    CategoryId = sc.CategoryId
                }).ToList()
            }).ToList();
        }

        // Get all subcategories
        public async Task<List<SubCategoryDto>> GetSubCategoriesAsync()
        {
            var subCategories = await _context.SubCategories.ToListAsync();

            return subCategories.Select(sc => new SubCategoryDto
            {
                Id = sc.Id,
                Name = sc.Name,
                MinPrice = sc.MinPrice,
                MaxPrice = sc.MaxPrice,
                CategoryId = sc.CategoryId
            }).ToList();
        }
    }
}
