namespace CorporateCashFlow.Business.IBusiness;

public interface ISecurityAuditRepository
{
    Task LogAsync(Guid? userId, string action, string outcome, string? ipAddress, string? detail);
}
