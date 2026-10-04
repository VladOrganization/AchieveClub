namespace AchieveClub.Server.Services
{
    public enum ProofCodePurpose
    {
        Registration,
        ChangePassword,
        ChangeEmail
    }

    public static class EmailTemplates
    {
        private record Content(string Subject, string Emoji, string Title, string Text, string Hint);

        private static Content GetContent(ProofCodePurpose purpose) => purpose switch
        {
            ProofCodePurpose.Registration => new Content(
                "Добро пожаловать в Byte School! Подтвердите почту",
                "🎉",
                "Добро пожаловать в Byte School!",
                "Остался один шаг — введите код на странице регистрации, чтобы подтвердить свою почту.",
                "Если вы не регистрировались в Byte School, просто проигнорируйте это письмо."),
            ProofCodePurpose.ChangePassword => new Content(
                "Byte School: код для смены пароля",
                "🔐",
                "Смена пароля",
                "Мы получили запрос на восстановление пароля. Введите код, чтобы задать новый.",
                "Если это были не вы, проигнорируйте письмо — ваш пароль останется прежним."),
            ProofCodePurpose.ChangeEmail => new Content(
                "Byte School: подтвердите новую почту",
                "✉️",
                "Подтвердите новую почту",
                "Вы хотите привязать этот адрес к своему аккаунту. Введите код в приложении, чтобы подтвердить смену.",
                "Если вы не меняли почту, проигнорируйте письмо — привязка не произойдёт."),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };

        public static (string Subject, string Html) ProofCode(ProofCodePurpose purpose, int code, int validMinutes, string? assetsUrl)
        {
            var c = GetContent(purpose);

            var digits =
                "<div style=\"display:inline-block;padding:14px 20px 14px 30px;font-size:38px;line-height:1.2;font-weight:700;" +
                "letter-spacing:10px;color:#c2410c;background:#fff7ed;border:2px solid #fed7aa;border-radius:14px;" +
                $"font-family:'Segoe UI',Arial,sans-serif;\">{code}</div>";

            string Img(string file, string alt) =>
                $"<img src=\"{assetsUrl}/{file}\" width=\"88\" height=\"88\" alt=\"{alt}\" style=\"display:block;border-radius:22px;border:0;box-shadow:0 6px 16px rgba(0,0,0,0.18);\">";

            var logo = string.IsNullOrWhiteSpace(assetsUrl)
                ? $"<div style=\"font-size:44px;line-height:1;\">{c.Emoji}</div>"
                : "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                  $"<td valign=\"middle\">{Img("logo.jpg", "Byte School")}</td>" +
                  "<td valign=\"middle\" style=\"padding:0 14px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                  "<td width=\"36\" height=\"36\" align=\"center\" valign=\"middle\" style=\"width:36px;height:36px;border-radius:18px;background:#ffffff;box-shadow:0 2px 8px rgba(0,0,0,0.15);\">" +
                  "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" align=\"center\" style=\"margin:0 auto;\">" +
                  "<tr><td width=\"6\" height=\"6\" style=\"font-size:0;line-height:0;\"></td><td width=\"4\" height=\"6\" bgcolor=\"#ea580c\" style=\"font-size:0;line-height:0;background:#ea580c;border-radius:2px 2px 0 0;\"></td><td width=\"6\" height=\"6\" style=\"font-size:0;line-height:0;\"></td></tr>" +
                  "<tr><td width=\"6\" height=\"4\" bgcolor=\"#ea580c\" style=\"font-size:0;line-height:0;background:#ea580c;border-radius:2px 0 0 2px;\"></td><td width=\"4\" height=\"4\" bgcolor=\"#ea580c\" style=\"font-size:0;line-height:0;background:#ea580c;\"></td><td width=\"6\" height=\"4\" bgcolor=\"#ea580c\" style=\"font-size:0;line-height:0;background:#ea580c;border-radius:0 2px 2px 0;\"></td></tr>" +
                  "<tr><td width=\"6\" height=\"6\" style=\"font-size:0;line-height:0;\"></td><td width=\"4\" height=\"6\" bgcolor=\"#ea580c\" style=\"font-size:0;line-height:0;background:#ea580c;border-radius:0 0 2px 2px;\"></td><td width=\"6\" height=\"6\" style=\"font-size:0;line-height:0;\"></td></tr>" +
                  "</table></td></tr></table></td>" +
                  $"<td valign=\"middle\">{Img("achieveclub.png", "Achieve Club")}</td>" +
                  "</tr></table>" +
                  "<div style=\"margin-top:14px;font-size:12px;letter-spacing:2px;text-transform:uppercase;color:#9a3412;font-weight:700;\">Byte School × Achieve Club</div>";

            var html = $$"""
<!DOCTYPE html>
<html lang="ru">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{c.Subject}}</title></head>
<body style="margin:0;padding:0;background:#fff7ed;font-family:'Segoe UI',Roboto,Arial,sans-serif;">
  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#fff7ed;padding:32px 12px;">
    <tr><td align="center">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(234,88,12,0.18);">
        <tr>
          <td align="center" style="background:#ffedd5;background:linear-gradient(135deg,#fed7aa,#ffedd5);padding:32px 24px 28px;">
            {{logo}}
          </td>
        </tr>
        <tr>
          <td style="padding:32px 28px 8px;text-align:center;">
            <h1 style="margin:0 0 12px;font-size:22px;color:#1f2937;">{{c.Title}}</h1>
            <p style="margin:0;font-size:15px;line-height:1.55;color:#4b5563;">{{c.Text}}</p>
          </td>
        </tr>
        <tr>
          <td align="center" style="padding:24px 28px 8px;">
            {{digits}}
            <p style="margin:16px 0 0;font-size:13px;color:#6b7280;">Код действителен {{validMinutes}} минут</p>
          </td>
        </tr>
        <tr>
          <td style="padding:24px 28px 32px;">
            <div style="border-top:1px solid #e5e7eb;padding-top:16px;font-size:12px;line-height:1.5;color:#9ca3af;text-align:center;">
              {{c.Hint}}<br>Никому не сообщайте этот код.
            </div>
          </td>
        </tr>
      </table>
      <p style="margin:16px 0 0;font-size:12px;color:#9ca3af;">© Byte School</p>
    </td></tr>
  </table>
</body>
</html>
""";
            return (c.Subject, html);
        }
    }
}
