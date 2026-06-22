using CorporateCashFlow.Entity;

namespace CorporateCashFlow.Business.IBusiness;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
}
