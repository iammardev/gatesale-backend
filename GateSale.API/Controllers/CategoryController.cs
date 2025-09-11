using GateSale.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly CategoryService _categoryService;

        public CategoryController(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET: api/category
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _categoryService.GetCategoriesAsync();
            return Ok(categories);
        }

        // GET: api/category/subcategories
        [HttpGet("subcategories")]
        public async Task<IActionResult> GetSubCategories()
        {
            var subCategories = await _categoryService.GetSubCategoriesAsync();
            return Ok(subCategories);
        }
    }
}
