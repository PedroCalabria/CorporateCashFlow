using CorporateCashFlow.Entity;
using CorporateCashFlow.Repository.Imp.Mapping;
using Microsoft.EntityFrameworkCore;

namespace CorporateCashFlow.Repository.Imp.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new UserRefreshTokenEntityTypeConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityAuditLogEntityTypeConfiguration());
    }
}
