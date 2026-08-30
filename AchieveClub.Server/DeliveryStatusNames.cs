using AchieveClub.Server.RepositoryItems;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server;

public static class DeliveryStatusNames
{
    public const string ReceivedTitle = "Получен студентом";
    public const string ReceivedColor = "10B981";
    public const string CancelledTitle = "Отклонён";
    public const string CancelledColor = "EF4444";

    public static bool IsCancelled(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        (title.Contains("отклон", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("отмен", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("cancel", StringComparison.OrdinalIgnoreCase));

    public static bool IsReceived(string? title) =>
        !string.IsNullOrWhiteSpace(title) &&
        (title.Contains("получен", StringComparison.OrdinalIgnoreCase) ||
         title.Contains("received", StringComparison.OrdinalIgnoreCase));

    public static async Task EnsureReceivedStatusAsync(ApplicationContext db)
    {
        var exists = await db.DeliveryStatuses.AnyAsync(s =>
            EF.Functions.ILike(s.Title, "%получен%") ||
            EF.Functions.ILike(s.Title, "%received%"));

        if (exists)
            return;

        db.DeliveryStatuses.Add(new DeliveryStatusDBO
        {
            Title = ReceivedTitle,
            Color = ReceivedColor
        });
        await db.SaveChangesAsync();
    }
}
