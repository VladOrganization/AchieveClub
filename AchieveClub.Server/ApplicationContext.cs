using AchieveClub.Server.RepositoryItems;
using Microsoft.EntityFrameworkCore;

namespace AchieveClub.Server;

public class ApplicationContext : DbContext
{
    public ApplicationContext(DbContextOptions options) : base(options)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public DbSet<UserDbo> Users { get; set; } = null!;
    public DbSet<AchievementDbo> Achievements { get; set; } = null!;
    public DbSet<CompletedAchievementDbo> CompletedAchievements { get; set; } = null!;
    public DbSet<RoleDbo> Roles { get; set; } = null!;
    public DbSet<CategoryDbo> Categories { get; set; } = null!;
    public DbSet<ProductDbo> Products { get; set; } = null!;
    public DbSet<VariantDbo> Variants { get; set; } = null!;
    public DbSet<ProductPhotoDbo> ProductPhotos { get; set; } = null!;
    public DbSet<OrderDBO> Orders { get; set; } = null!;
    public DbSet<DeliveryStatusDBO> DeliveryStatuses { get; set; } = null!;
    public DbSet<TagDbo> Tags { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<UserDbo>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleRefId)
            .HasPrincipalKey(r => r.Id);

        modelBuilder
            .Entity<CompletedAchievementDbo>()
            .HasOne(ca => ca.User);
        modelBuilder
            .Entity<CompletedAchievementDbo>()
            .HasOne(ca => ca.Supervisor);
        modelBuilder
            .Entity<CompletedAchievementDbo>()
            .HasIndex(ca => new { ca.UserRefId, ca.AchieveRefId })
            .IsUnique();

        modelBuilder
            .Entity<VariantDbo>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Variants);

        modelBuilder
            .Entity<ProductPhotoDbo>()
            .HasOne(p => p.Variant)
            .WithMany(v => v.ProductPhotos);
    }
}