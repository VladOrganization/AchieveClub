using AchieveClub.Server.ApiContracts.Categories.Request;
using AchieveClub.Server.ApiContracts.Categories.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(ApplicationContext db) : ControllerBase
{
    [HttpGet]
    [OutputCache(Duration = (3 * 60), Tags = ["achievements"])]
    public async Task<ActionResult<List<SmallCategoryResponse>>> GetAll()
    {
        var categories = await db.Categories.ToListAsync();

        return categories
            .Select(category =>
            {
                var available = (category.StartDate == null || category.EndDate == null ||
                                 category.StartDate <= DateTime.Now && category.EndDate >= DateTime.Now) &&
                                db.Products.Any(p => p.CategoryId == category.Id);
                return new SmallCategoryResponse(
                    category.Id,
                    category.Title,
                    category.Color,
                    category.StartDate,
                    category.EndDate,
                    available ? category.AvailableBanner : category.UnavailableBanner,
                    available
                );
            })
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var newCategory = new CategoryDbo
        {
            Title = request.Title,
            Color = request.Color,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AvailableBanner = request.AvailableBanner,
            UnavailableBanner = request.UnavailableBanner
        };

        db.Categories.Add(newCategory);

        await db.SaveChangesAsync();

        return Created();
    }

    [HttpDelete("{categoryId}")]
    public async Task<ActionResult> DeleteCategory([FromRoute] int categoryId)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
            return NotFound();

        db.Categories.Remove(category);

        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{categoryId}")]
    public async Task<ActionResult> UpdateCategory([FromBody] CreateCategoryRequest request, [FromRoute] int categoryId)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
            return NotFound();

        category.Title = request.Title;
        category.Color = request.Color;
        category.StartDate = request.StartDate;
        category.EndDate = request.EndDate;
        category.AvailableBanner = request.AvailableBanner;
        category.UnavailableBanner = request.UnavailableBanner;

        await db.SaveChangesAsync();

        return NoContent();
    }
}