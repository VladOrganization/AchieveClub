using AchieveClub.Server.ApiContracts.Categories.Request;
using AchieveClub.Server.ApiContracts.Categories.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(
    ILogger<CategoriesController> logger,
    ApplicationContext db,
    IOutputCacheStore cache) : ControllerBase
{
    private const int BannerMaxWidth = 1920;
    private const int BannerMaxHeight = 1080;

    [HttpGet]
    [OutputCache(Duration = (3 * 60), Tags = ["achievements"])]
    public async Task<ActionResult<List<SmallCategoryResponse>>> GetShown()
    {
        var categories = await db.Categories.Where(c => c.Show).ToListAsync();

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

    /// <summary>Все категории (включая скрытые) со всеми полями — для админки.</summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<AdminCategoryResponse>>> GetAll(CancellationToken ct)
    {
        return await db.Categories
            .OrderBy(c => c.Id)
            .Select(c => new AdminCategoryResponse(
                c.Id,
                c.Title,
                c.Color,
                c.StartDate,
                c.EndDate,
                c.AvailableBanner,
                c.UnavailableBanner,
                c.Show,
                db.Products.Count(p => p.CategoryId == c.Id)))
            .ToListAsync(ct);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required");

        var newCategory = new CategoryDbo
        {
            Title = request.Title.Trim(),
            Color = request.Color,
            StartDate = ToUnspecified(request.StartDate),
            EndDate = ToUnspecified(request.EndDate),
            AvailableBanner = request.AvailableBanner,
            UnavailableBanner = request.UnavailableBanner,
            Show = request.Show
        };

        db.Categories.Add(newCategory);

        await db.SaveChangesAsync();
        await cache.EvictByTagAsync("achievements", default);

        return Created();
    }

    [HttpDelete("{categoryId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteCategory([FromRoute] int categoryId)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
            return NotFound();

        db.Categories.Remove(category);

        await db.SaveChangesAsync();
        await cache.EvictByTagAsync("achievements", default);

        return NoContent();
    }

    [HttpPut("{categoryId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateCategory([FromBody] CreateCategoryRequest request, [FromRoute] int categoryId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required");

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null)
            return NotFound();

        category.Title = request.Title.Trim();
        category.Color = request.Color;
        category.StartDate = ToUnspecified(request.StartDate);
        category.EndDate = ToUnspecified(request.EndDate);
        category.AvailableBanner = request.AvailableBanner;
        category.UnavailableBanner = request.UnavailableBanner;
        category.Show = request.Show;

        await db.SaveChangesAsync();
        await cache.EvictByTagAsync("achievements", default);

        return NoContent();
    }

    /// <summary>
    /// Загружает баннер категории в wwwroot/banners и возвращает относительный путь,
    /// который нужно передать в AvailableBanner / UnavailableBanner.
    /// Изображение вписывается в 1920x1080 с сохранением пропорций (меньшие не увеличиваются).
    /// </summary>
    [HttpPost("banners")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<string>> UploadBanner(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            logger.LogWarning("No file uploaded");
            return BadRequest("No file uploaded");
        }

        if (file.Length > 10_000_000)
        {
            logger.LogWarning("File it too long: {file.Length} bytes", file.Length);
            return BadRequest($"File it too long: {file.Length} bytes");
        }

        var extension = Path.GetExtension(file.FileName).ToLower();
        var fileTypes = new List<string> { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };
        if (fileTypes.Contains(extension) == false)
        {
            logger.LogWarning("File extension not supported: {extension}", extension);
            return BadRequest($"File extension not supported: {extension}. Supported extensions: {string.Join(",", fileTypes)}");
        }

        var filePath = $"banners/{Guid.NewGuid()}.webp";
        Directory.CreateDirectory("./wwwroot/banners");

        try
        {
            using var readStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(readStream, ct);

            image.Mutate(x => x.AutoOrient());

            if (image.Width > BannerMaxWidth || image.Height > BannerMaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(BannerMaxWidth, BannerMaxHeight),
                    Mode = ResizeMode.Max
                }));
            }

            await using var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write);
            await image.SaveAsWebpAsync(fileStream, ct);
        }
        catch (UnknownImageFormatException)
        {
            logger.LogWarning("File is not a valid image: {fileName}", file.FileName);
            return BadRequest("File is not a valid image");
        }

        logger.LogInformation("Category banner saved as .WEBP on: {filePath}", filePath);
        return Ok(filePath);
    }

    // Колонки timestamp without time zone: Npgsql не принимает DateTime с Kind=Utc
    private static DateTime? ToUnspecified(DateTime? value) => value == null
        ? null
        : DateTime.SpecifyKind(value.Value.Kind == DateTimeKind.Utc ? value.Value.ToLocalTime() : value.Value,
            DateTimeKind.Unspecified);
}
