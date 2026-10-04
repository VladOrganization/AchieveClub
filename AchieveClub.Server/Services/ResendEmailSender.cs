using AchieveClub.Server.Auth;

namespace AchieveClub.Server.Services
{
    public class ResendEmailSender(HttpClient http, EmailSettings settings, ILogger<ResendEmailSender> logger)
    {
        public async Task<bool> SendAsync(string emailAddress, string subject, string htmlContent, CancellationToken ct = default)
        {
            var payload = new
            {
                from = $"{settings.Name} <{settings.Email}>",
                to = new[] { emailAddress },
                subject,
                html = htmlContent
            };

            try
            {
                using var response = await http.PostAsJsonAsync("emails", payload, ct);
                if (response.IsSuccessStatusCode)
                    return true;

                logger.LogError("Resend returned {statusCode}: {body}",
                    (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                return false;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to send email via Resend. Email: {emailAddress}", emailAddress);
                return false;
            }
        }
    }
}
