using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AchieveClub.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BannersController(ILogger<BannersController> logger) : ControllerBase
    {
        [HttpPost("fullscreen")]
        public async Task<ActionResult> UploadFullscreenBanner(IFormFile file)
        {
            return await Upload(file, new Size(1080, 1920));
        }

        [HttpPost("line")]
        public async Task<ActionResult> UploadLineBanner(IFormFile file)
        {
            return await Upload(file, new Size(300, 120));
        }

        public async Task<ActionResult> Upload(IFormFile file, Size imageSize)
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

            var fileTypes = new List<string> { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

            if (fileTypes.Contains(fileInfo.Extension) == false)
            {
                logger.LogWarning("File extension not supported: {fileInfo.Extension}. Supported extensions: {fileTypes}", fileInfo.Extension, fileTypes);
                return BadRequest($"File extension not supported: {fileInfo.Extension}. Supported extensions: {fileTypes.Aggregate((a, b) => $"{a},{b}")}");
            }

            var filePath = $"icons/banners/{Path.GetFileNameWithoutExtension(fileInfo.Name)}.webp";

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
                    Size = imageSize,
                    Mode = ResizeMode.Crop
                }));

                using (var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write))
                {
                    await image.SaveAsWebpAsync(fileStream);
                }
            }

            logger.LogInformation("File saved as .WEBP on: {filePath}", filePath);
            return Ok(filePath);
        }
    }
}
