using CorporateCashFlow.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CorporateCashFlow.Repository.Imp.Mapping;

public class SecurityAuditLogEntityTypeConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        builder.ToTable("SecurityAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).UseIdentityColumn();
        builder.Property(l => l.Action).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.Detail).HasMaxLength(500);
    }
}
