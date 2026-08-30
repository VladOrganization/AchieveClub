using System.Linq.Expressions;
using AchieveClub.Server.RepositoryItems;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public static Task<DeliveryStatusDBO> EnsureReceivedStatusAsync(ApplicationContext db) =>
        GetOrCreateAsync(
            db,
            s => EF.Functions.ILike(s.Title, "%получен%") || EF.Functions.ILike(s.Title, "%received%"),
            ReceivedTitle,
            ReceivedColor);

    public static Task<DeliveryStatusDBO> EnsureCancelledStatusAsync(ApplicationContext db) =>
        GetOrCreateAsync(
            db,
            s => EF.Functions.ILike(s.Title, "%отклон%") ||
                 EF.Functions.ILike(s.Title, "%отмен%") ||
                 EF.Functions.ILike(s.Title, "%cancel%"),
            CancelledTitle,
            CancelledColor);

    private static async Task<DeliveryStatusDBO> GetOrCreateAsync(
        ApplicationContext db,
        Expression<Func<DeliveryStatusDBO, bool>> predicate,
        string title,
        string color)
    {
        var existing = await db.DeliveryStatuses.FirstOrDefaultAsync(predicate);
        if (existing != null)
            return existing;

        await SyncIdSequenceAsync(db);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var nextId = await db.DeliveryStatuses.MaxAsync(s => (int?)s.Id) ?? 0;
            var created = new DeliveryStatusDBO
            {
                Id = nextId + 1,
                Title = title,
                Color = color
            };
            db.DeliveryStatuses.Add(created);
            try
            {
                await db.SaveChangesAsync();
                return created;
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                db.Entry(created).State = EntityState.Detached;
                existing = await db.DeliveryStatuses.AsNoTracking().FirstOrDefaultAsync(predicate);
                if (existing != null)
                    return await db.DeliveryStatuses.FirstAsync(s => s.Id == existing.Id);

                await SyncIdSequenceAsync(db);
            }
        }

        return await db.DeliveryStatuses.FirstAsync(predicate);
    }

    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static Task SyncIdSequenceAsync(ApplicationContext db) =>
        db.Database.ExecuteSqlRawAsync(
            """
            SELECT setval(
                pg_get_serial_sequence('"DeliveryStatuses"', 'Id'),
                COALESCE((SELECT MAX("Id") FROM "DeliveryStatuses"), 1),
                (SELECT EXISTS (SELECT 1 FROM "DeliveryStatuses"))
            )
            """);
}
