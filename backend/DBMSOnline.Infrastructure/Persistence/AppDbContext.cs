using Microsoft.EntityFrameworkCore;
using DBMSOnline.Domain.Entities;

namespace DBMSOnline.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SavedConnection> SavedConnections => Set<SavedConnection>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<QueryHistory> QueryHistories => Set<QueryHistory>();
    public DbSet<DatabaseBackup> DatabaseBackups => Set<DatabaseBackup>();
    public DbSet<SavedReport> SavedReports => Set<SavedReport>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var superAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var orgAdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasData(new Tenant
            {
                Id = defaultTenantId,
                Name = "Enterprise Global Workspace",
                Domain = "enterprise.local",
                PlanType = "Enterprise",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasData(
                new User
                {
                    Id = superAdminId,
                    TenantId = defaultTenantId,
                    Email = "owner@dbmsonline.com",
                    PasswordHash = "OwnerPass123!", // In production: SHA256 / BCrypt
                    FullName = "Super Admin Owner (Platform)",
                    Role = Domain.Enums.UserRole.OwnerSuperAdmin,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new User
                {
                    Id = orgAdminId,
                    TenantId = defaultTenantId,
                    Email = "admin@enterprise.com",
                    PasswordHash = "OrgPass123!",
                    FullName = "Organization Team Admin",
                    Role = Domain.Enums.UserRole.OrgAdmin,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SavedConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<QueryHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExecutedAt);
        });
    }
}
