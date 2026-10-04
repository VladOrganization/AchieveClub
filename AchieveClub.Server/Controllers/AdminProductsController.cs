using AchieveClub.Server.ApiContracts.Products.Request;
using AchieveClub.Server.ApiContracts.Products.Response;
using AchieveClub.Server.RepositoryItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AchieveClub.Server.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
public class AdminProductsController(
    ILogger<AdminProductsController> logger,
    ApplicationContext db,
    IOutputCacheStore cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminProductResponse>>> GetAll(CancellationToken ct)
    {
        var products = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)!
            .ThenInclude(v => v.ProductPhotos)
            .AsSplitQuery()
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        return products.Select(ToResponse).ToList();
    }

    [HttpGet("{productId:int}")]
    public async Task<ActionResult<AdminProductResponse>> GetById([FromRoute] int productId, CancellationToken ct)
    {
        var product = await LoadProduct(productId, ct);
        if (product == null)
            return NotFound($"Product with productId:{productId} not found");

        return ToResponse(product);
    }

    [HttpPost]
    public async Task<ActionResult<AdminProductResponse>> Create([FromBody] AdminProductRequest request, CancellationToken ct)
    {
        var error = await Validate(request, ct);
        if (error != null)
            return BadRequest(error);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var variants = request.Variants.Select(NewVariant).ToList();
        var product = new ProductDbo
        {
            Type = request.Type,
            Name = request.Name,
            Details = request.Details,
            Price = request.Price,
            CategoryId = request.CategoryId,
            Variants = variants
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        // Id вариантов и фото известны только после первого сохранения
        ApplyDefaults(product, request, variants);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.EvictByTagAsync("achievements", ct);

        logger.LogInformation("Product created: {productId}", product.Id);
        return CreatedAtAction(nameof(GetById), new { productId = product.Id }, ToResponse((await LoadProduct(product.Id, ct))!));
    }

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<AdminProductResponse>> Update([FromRoute] int productId, [FromBody] AdminProductRequest request, CancellationToken ct)
    {
        var product = await LoadProduct(productId, ct);
        if (product == null)
            return NotFound($"Product with productId:{productId} not found");

        var error = await Validate(request, ct);
        if (error != null)
            return BadRequest(error);

        var existing = product.Variants!.ToDictionary(v => v.Id);
        var unknown = request.Variants.FirstOrDefault(v => v.Id.HasValue && existing.ContainsKey(v.Id.Value) == false);
        if (unknown != null)
            return BadRequest($"Variant with id:{unknown.Id} does not belong to product:{productId}");

        var keptIds = request.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        var removed = product.Variants!.Where(v => keptIds.Contains(v.Id) == false).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(v => v.Id).ToList();
            if (await db.Orders.AnyAsync(o => removedIds.Contains(o.VariantId), ct))
                return Conflict("Cannot remove variants that are used in orders");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // сначала разрываем циклические ссылки на данные, которые будут заменены
        product.DefaultVariantId = null;
        foreach (var v in product.Variants!)
            v.DefaultPhotoId = null;
        await db.SaveChangesAsync(ct);

        product.Type = request.Type;
        product.Name = request.Name;
        product.Details = request.Details;
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;

        foreach (var v in removed)
        {
            db.ProductPhotos.RemoveRange(v.ProductPhotos!);
            db.Variants.Remove(v);
            product.Variants.Remove(v);
        }

        var ordered = new List<VariantDbo>();
        foreach (var vr in request.Variants)
        {
            if (vr.Id.HasValue)
            {
                var variant = existing[vr.Id.Value];
                variant.Name = vr.Name;
                variant.Color = vr.Color;
                variant.Quantity = vr.Quantity;
                db.ProductPhotos.RemoveRange(variant.ProductPhotos!);
                variant.ProductPhotos = vr.Photos.Select(u => new ProductPhotoDbo { Url = u }).ToList();
                ordered.Add(variant);
            }
            else
            {
                var variant = NewVariant(vr);
                product.Variants.Add(variant);
                ordered.Add(variant);
            }
        }
        await db.SaveChangesAsync(ct);

        ApplyDefaults(product, request, ordered);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.EvictByTagAsync("achievements", ct);

        logger.LogInformation("Product updated: {productId}", productId);
        return ToResponse((await LoadProduct(productId, ct))!);
    }

    [HttpDelete("{productId:int}")]
    public async Task<ActionResult> Delete([FromRoute] int productId, CancellationToken ct)
    {
        var product = await LoadProduct(productId, ct);
        if (product == null)
            return NotFound($"Product with productId:{productId} not found");

        if (await db.Orders.AnyAsync(o => o.ProductId == productId, ct))
            return Conflict("Cannot delete a product that is used in orders");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        product.DefaultVariantId = null;
        foreach (var v in product.Variants!)
            v.DefaultPhotoId = null;
        await db.SaveChangesAsync(ct);

        foreach (var v in product.Variants!)
            db.ProductPhotos.RemoveRange(v.ProductPhotos!);
        db.Variants.RemoveRange(product.Variants!);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await cache.EvictByTagAsync("achievements", ct);

        logger.LogInformation("Product deleted: {productId}", productId);
        return NoContent();
    }

    /// <summary>
    /// Загружает фото товара в wwwroot/products и возвращает относительный путь,
    /// который нужно передать в Photos при создании или редактировании товара.
    /// </summary>
    [HttpPost("photos")]
    public async Task<ActionResult<string>> UploadPhoto(IFormFile file, CancellationToken ct)
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

        var filePath = $"products/{Guid.NewGuid()}.webp";
        Directory.CreateDirectory("./wwwroot/products");

        try
        {
            using var readStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(readStream, ct);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1200, 1200),
                Mode = ResizeMode.Max
            }));

            await using var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write);
            await image.SaveAsWebpAsync(fileStream, ct);
        }
        catch (UnknownImageFormatException)
        {
            logger.LogWarning("File is not a valid image: {fileName}", file.FileName);
            return BadRequest("File is not a valid image");
        }

        logger.LogInformation("Product photo saved as .WEBP on: {filePath}", filePath);
        return Ok(filePath);
    }

    private Task<ProductDbo?> LoadProduct(int productId, CancellationToken ct) =>
        db.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)!
            .ThenInclude(v => v.ProductPhotos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

    private async Task<string?> Validate(AdminProductRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required";
        if (string.IsNullOrWhiteSpace(request.Type)) return "Type is required";
        if (request.Price < 0) return "Price must be >= 0";
        if (request.Variants == null || request.Variants.Count == 0) return "At least one variant is required";
        if (request.DefaultVariantIndex < 0 || request.DefaultVariantIndex >= request.Variants.Count)
            return "DefaultVariantIndex is out of range";
        if (await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct) == false)
            return $"Category with categoryId:{request.CategoryId} not found";

        foreach (var v in request.Variants)
        {
            if (string.IsNullOrWhiteSpace(v.Name)) return "Variant name is required";
            if (v.Quantity < 0) return "Variant quantity must be >= 0";
            if (v.Photos == null) return "Variant photos list is required";
            if (v.Photos.Count > 0 && (v.DefaultPhotoIndex < 0 || v.DefaultPhotoIndex >= v.Photos.Count))
                return "DefaultPhotoIndex is out of range";
        }

        var ids = request.Variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToList();
        if (ids.Count != ids.Distinct().Count()) return "Duplicate variant ids";

        return null;
    }

    private static VariantDbo NewVariant(AdminVariantRequest v) => new()
    {
        Name = v.Name,
        Color = v.Color,
        Quantity = v.Quantity,
        ProductPhotos = v.Photos.Select(u => new ProductPhotoDbo { Url = u }).ToList()
    };

    /// <param name="variants">Варианты в том же порядке, что и request.Variants</param>
    private static void ApplyDefaults(ProductDbo product, AdminProductRequest request, List<VariantDbo> variants)
    {
        for (var i = 0; i < variants.Count; i++)
        {
            var photos = variants[i].ProductPhotos!;
            variants[i].DefaultPhotoId = photos.Count > 0 ? photos[request.Variants[i].DefaultPhotoIndex].Id : null;
        }

        product.DefaultVariantId = variants[request.DefaultVariantIndex].Id;
    }

    private static AdminProductResponse ToResponse(ProductDbo p) => new(
        p.Id, p.Type, p.Name, p.Details, p.Price, p.CategoryId, p.Category?.Title,
        p.Variants!.Select(v => new AdminVariantResponse(
            v.Id, v.Name, v.Color, v.Quantity, v.Id == p.DefaultVariantId,
            v.ProductPhotos!.Select(ph => new VariantPhotoResponse(ph.Id == v.DefaultPhotoId, ph.Url)).ToList()
        )).ToList());
}
