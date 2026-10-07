using AchieveClub.Server.ApiContracts.Avatars.Request;
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
        private const string PresetsFolder = "avatars/presets";
        private static readonly List<string> FileTypes = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var userIdString = HttpContext.User.Identity?.Name;
            if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
            {
                logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}", userIdString);
                return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
            }
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

            var validationError = ValidateImage(file);
            if (validationError != null)
                return validationError;

            var filePath = $"avatars/{Guid.NewGuid()}.jpeg";

            if (Path.Exists($"./wwwroot/{filePath}"))
            {
                logger.LogWarning("File with this name already exists: {filePath}", filePath);
                return BadRequest($"File with this name already exists: {filePath}");
            }

            using (var readStream = file.OpenReadStream())
            {
                var image = await Image.LoadAsync(readStream);

                image.Mutate(x => x.Resize(new ResizeOptions()
                {
                    Size = new Size(600, 600),
                    Mode = ResizeMode.Crop
                }));

                using (var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write))
                {
                    await image.SaveAsJpegAsync(fileStream);
                }
            }

            logger.LogInformation("File saved as .jpeg on: {filePath}", filePath);

            user.Avatar = filePath;
            await db.SaveChangesAsync();
            await cache.EvictByTagAsync("users", default);

            logger.LogInformation("User avatar changed. User: {user}", user);

            return Ok(filePath);
        }

        /// <summary>Список предзагруженных аватарок, из которых пользователь может выбрать свою</summary>
        [HttpGet("presets")]
        public ActionResult<List<string>> GetPresets()
        {
            Directory.CreateDirectory($"./wwwroot/{PresetsFolder}");

            return new DirectoryInfo($"./wwwroot/{PresetsFolder}")
                .GetFiles()
                .Where(f => FileTypes.Contains(f.Extension.ToLower()))
                .OrderBy(f => f.CreationTimeUtc)
                .ThenBy(f => f.Name)
                .Select(f => $"{PresetsFolder}/{f.Name}")
                .ToList();
        }

        /// <summary>Ставит текущему пользователю одну из предзагруженных аватарок</summary>
        [Authorize]
        [HttpPut("presets/select")]
        public async Task<IActionResult> SelectPreset(SelectAvatarPresetRequest model)
        {
            var userIdString = HttpContext.User.Identity?.Name;
            if (userIdString == null || int.TryParse(userIdString, out int userId) == false)
            {
                logger.LogWarning("Access token not contains userId or userId is the wrong format: {userIdString}", userIdString);
                return NotFound($"Access token not contains userId or userId is the wrong format: {userIdString}");
            }
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                logger.LogWarning("User with userId:{userId} not found", userId);
                return NotFound($"User with userId:{userId} not found");
            }

            if (PresetExists(model.Path) == false)
            {
                logger.LogWarning("Avatar preset not found: {path}", model.Path);
                return NotFound($"Avatar preset not found: {model.Path}");
            }

            user.Avatar = model.Path;
            await db.SaveChangesAsync();
            await cache.EvictByTagAsync("users", default);

            logger.LogInformation("User avatar changed to preset {filePath}. User: {user}", model.Path, user);

            return Ok(model.Path);
        }

        /// <summary>Путь указывает на существующую предзагруженную аватарку (вида "avatars/presets/имя.webp")</summary>
        public static bool PresetExists(string path) =>
            path == $"{PresetsFolder}/{Path.GetFileName(path)}"
            && FileTypes.Contains(Path.GetExtension(path).ToLower())
            && System.IO.File.Exists($"./wwwroot/{path}");

        /// <summary>Добавляет аватарку в список предзагруженных</summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("presets")]
        public async Task<IActionResult> UploadPreset(IFormFile file)
        {
            var validationError = ValidateImage(file);
            if (validationError != null)
                return validationError;

            Directory.CreateDirectory($"./wwwroot/{PresetsFolder}");

            var filePath = $"{PresetsFolder}/{Guid.NewGuid()}.webp";

            using (var readStream = file.OpenReadStream())
            {
                var image = await Image.LoadAsync(readStream);

                image.Mutate(x => x.Resize(new ResizeOptions()
                {
                    Size = new Size(600, 600),
                    Mode = ResizeMode.Crop
                }));

                using (var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write))
                {
                    await image.SaveAsWebpAsync(fileStream);
                }
            }

            logger.LogInformation("Avatar preset saved as .WEBP on: {filePath}", filePath);
            return Ok(filePath);
        }

        /// <summary>Удаляет аватарку из списка предзагруженных. У пользователей с этой аватаркой она сбрасывается</summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("presets/{fileName}")]
        public async Task<IActionResult> DeletePreset(string fileName)
        {
            if (Path.GetFileName(fileName) != fileName || FileTypes.Contains(Path.GetExtension(fileName).ToLower()) == false)
            {
                logger.LogWarning("Wrong avatar preset file name: {fileName}", fileName);
                return BadRequest($"Wrong avatar preset file name: {fileName}");
            }

            var filePath = $"{PresetsFolder}/{fileName}";

            if (System.IO.File.Exists($"./wwwroot/{filePath}") == false)
            {
                logger.LogWarning("Avatar preset not found: {filePath}", filePath);
                return NotFound($"Avatar preset not found: {filePath}");
            }

            var resetCount = await db.Users
                .Where(u => u.Avatar == filePath)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Avatar, (string?)null));

            System.IO.File.Delete($"./wwwroot/{filePath}");
            await cache.EvictByTagAsync("users", default);

            logger.LogInformation("Avatar preset deleted: {filePath}. Avatar reset for {resetCount} users", filePath, resetCount);
            return Ok();
        }

        private BadRequestObjectResult? ValidateImage(IFormFile file)
        {
            if (file.Length == 0)
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

            if (FileTypes.Contains(fileInfo.Extension.ToLower()) == false)
            {
                logger.LogWarning("File extension not supported: {fileInfo.Extension}. Supported extensions: {fileTypes}", fileInfo.Extension, FileTypes);
                return BadRequest($"File extension not supported: {fileInfo.Extension}. Supported extensions: {FileTypes.Aggregate((a, b) => $"{a},{b}")}");
            }

            return null;
        }
    }
}
