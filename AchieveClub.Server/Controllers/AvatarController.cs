using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AchieveClub.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AvatarController(ApplicationContext db, ILogger<AvatarController> logger, IOutputCacheStore cache) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
        {
            var userIdString = HttpContext.User.Identity?.Name;
            if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
            {
                logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}", userIdString);
                return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
            }
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

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

            var fileInfo = new FileInfo(file.FileName);

            if (string.IsNullOrWhiteSpace(fileInfo.Extension))
            {
                logger.LogWarning("File extension not found: {file.FileName}", file.FileName);
                return BadRequest($"File extension not found: {file.FileName}");
            }

            var fileTypes = new List<string> { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

            if (fileTypes.Contains(fileInfo.Extension.ToLower()) == false)
            {
                logger.LogWarning("File extension not supported: {fileInfo.Extension}. Supported extensions: {fileTypes}", fileInfo.Extension, fileTypes);
                return BadRequest($"File extension not supported: {fileInfo.Extension}. Supported extensions: {fileTypes.Aggregate((a, b) => $"{a},{b}")}");
            }

            var filePath = $"avatars/{Guid.NewGuid()}.jpeg";
            Directory.CreateDirectory("./wwwroot/avatars");

            if (Path.Exists($"./wwwroot/{filePath}"))
            {
                logger.LogWarning("File with this name already exists: {filePath}", filePath);
                return BadRequest($"File with this name already exists: {filePath}");
            }

            using (var readStream = file.OpenReadStream())
            using (var image = await Image.LoadAsync(readStream, ct))
            {
                image.Mutate(x => x.Resize(new ResizeOptions()
                {
                    Size = new Size(600, 600),
                    Mode = ResizeMode.Crop
                }));

                await using var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write);
                await image.SaveAsJpegAsync(fileStream, ct);
            }

            logger.LogInformation("File saved as .jpeg on: {filePath}", filePath);

            var previousAvatar = user.Avatar;
            user.Avatar = filePath;
            await db.SaveChangesAsync(ct);
            await cache.EvictByTagAsync("users", ct);

            TryDeletePreviousAvatar(previousAvatar);

            logger.LogInformation("User avatar changed. User: {user}", user);

            return Ok(filePath);
        }

        private static void TryDeletePreviousAvatar(string? previousAvatar)
        {
            if (string.IsNullOrWhiteSpace(previousAvatar))
                return;

            var relative = previousAvatar.Replace('\\', '/').TrimStart('/');
            if (!relative.StartsWith("avatars/", StringComparison.OrdinalIgnoreCase))
                return;
            if (relative.Contains("..", StringComparison.Ordinal))
                return;

            var fullPath = Path.GetFullPath(Path.Combine("./wwwroot", relative));
            var avatarsRoot = Path.GetFullPath("./wwwroot/avatars");
            if (!fullPath.StartsWith(avatarsRoot, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch
            {
                // leftover file is not fatal
            }
        }
    }
}
