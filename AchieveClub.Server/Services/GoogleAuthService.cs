using System.Text.RegularExpressions;
using AchieveClub.Server.Auth;
using Google.Apis.Auth;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AchieveClub.Server.Services
{
    public record GoogleProfile(string Email, string FirstName, string LastName, string? PictureUrl);

    public class GoogleAuthService(
        GoogleSettings settings,
        HttpClient http,
        ILogger<GoogleAuthService> logger)
    {
        /// <summary>Проверяет подпись/audience/срок ID-токена Google. null — токен невалиден или email не подтвержден.</summary>
        public async Task<GoogleProfile?> ValidateAsync(string idToken)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = [settings.ClientId] });
            }
            catch (InvalidJwtException ex)
            {
                logger.LogWarning("Invalid Google id token: {message}", ex.Message);
                return null;
            }

            if (payload.EmailVerified == false || string.IsNullOrWhiteSpace(payload.Email))
            {
                logger.LogWarning("Google account email is not verified");
                return null;
            }

            var (first, last) = SplitName(payload);
            return new GoogleProfile(payload.Email, first, last, payload.Picture);
        }

        private static (string, string) SplitName(GoogleJsonWebSignature.Payload payload)
        {
            var first = payload.GivenName?.Trim();
            var last = payload.FamilyName?.Trim();

            if (string.IsNullOrEmpty(first) && !string.IsNullOrWhiteSpace(payload.Name))
            {
                var parts = payload.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                first = parts[0];
                last ??= parts.Length > 1 ? parts[1] : null;
            }

            first = string.IsNullOrEmpty(first) ? payload.Email.Split('@')[0] : first;
            return (Truncate(first, 100), Truncate(last ?? "", 100));
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>Скачивает аватарку Google и сохраняет так же, как AvatarController. Возвращает относительный путь или null.</summary>
        public async Task<string?> DownloadAvatarAsync(string? pictureUrl, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(pictureUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                // Google отдает маленькое превью (=s96-c); просим размер побольше
                var url = Regex.Replace(uri.AbsoluteUri, @"=s\d+(-c)?$", "=s600-c");

                using var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) return null;

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var image = await Image.LoadAsync(stream, ct);
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(600, 600), Mode = ResizeMode.Crop }));

                var filePath = $"avatars/{Guid.NewGuid()}.jpeg";
                Directory.CreateDirectory("./wwwroot/avatars");
                await using var fileStream = new FileStream($"./wwwroot/{filePath}", FileMode.CreateNew, FileAccess.Write);
                await image.SaveAsJpegAsync(fileStream, ct);
                return filePath;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not download Google avatar");
                return null;
            }
        }
    }
}
