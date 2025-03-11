using Microsoft.AspNetCore.Mvc;
using pentyflixApi.Models.Reddit;
using pentyflixApi.Services;

namespace pentyflixApi.Controllers.Reddit;

[ApiController]
[Route("api/reddit/[controller]")]
public class CategoryController : ControllerBase
{
    private readonly RedditCategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(RedditCategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet("popular")]
    public async Task<ActionResult<List<RedditCategory>>> GetPopularCategories([FromQuery] int limit = 25)
    {
        try
        {
            if (limit <= 0 || limit > 100)
            {
                limit = 25; // Default limit if invalid
            }
            
            _logger.LogInformation($"Processing request for popular categories, limit: {limit}");
            
            var categories = await _categoryService.GetPopularCategories(limit);
            
            if (categories.Count == 0)
            {
                return NotFound("No categories found");
            }
            
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving popular categories");
            return StatusCode(500, new { error = "An error occurred while retrieving popular categories", details = ex.Message });
        }
    }
    
    [HttpGet("search")]
    public async Task<ActionResult<List<RedditCategory>>> SearchCategories(
        [FromQuery] string query, 
        [FromQuery] int limit = 25)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }
            
            if (limit <= 0 || limit > 100)
            {
                limit = 25; // Default limit if invalid
            }
            
            _logger.LogInformation($"Processing search request for '{query}', limit: {limit}");
            
            var categories = await _categoryService.SearchCategories(query, limit);
            
            if (categories.Count == 0)
            {
                return NotFound($"No categories found matching '{query}'");
            }
            
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching for categories with query: '{query}'");
            return StatusCode(500, new { error = $"An error occurred while searching for '{query}'", details = ex.Message });
        }
    }
    
    [HttpGet("{categoryName}")]
    public async Task<ActionResult<RedditCategory>> GetCategoryDetails(string categoryName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return BadRequest("Category name is required");
            }
            
            _logger.LogInformation($"Processing request for category details: {categoryName}");
            
            var category = await _categoryService.GetCategoryDetails(categoryName);
            
            return Ok(category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving details for category: {categoryName}");
            return StatusCode(500, new { error = $"An error occurred while retrieving details for '{categoryName}'", details = ex.Message });
        }
    }
}