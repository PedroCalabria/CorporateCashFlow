using CorporateCashFlow.Business.IBusiness;
using CorporateCashFlow.Entity;
using CorporateCashFlow.Repository.Imp.Context;

namespace CorporateCashFlow.Repository.Imp.Repository;

public class SecurityAuditRepository : ISecurityAuditRepository
{
    private readonly AppDbContext _dbContext;

    public SecurityAuditRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(Guid? userId, string action, string outcome, string? ipAddress, string? detail)
    {
        try
        {
            var log = new SecurityAuditLog
            {
                UserId = userId,
                Action = action,
                Outcome = outcome,
                IpAddress = ipAddress,
                Detail = detail,
                OccurredAt = DateTimeOffset.UtcNow
            };

            await _dbContext.SecurityAuditLogs.AddAsync(log);
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // Audit failure must never propagate to caller
        }
    }
}
