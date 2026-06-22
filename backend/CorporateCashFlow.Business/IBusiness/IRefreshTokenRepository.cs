using CorporateCashFlow.Entity;

namespace CorporateCashFlow.Business.IBusiness;

public interface IRefreshTokenRepository
{
    Task<UserRefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<int> ConsumeAndRotateAsync(Guid oldTokenId, UserRefreshToken newToken);
    Task RevokeAllByFamilyAsync(Guid familyId);
    Task RevokeAllByUserIdAsync(Guid userId);
    Task AddAsync(UserRefreshToken token);
}
