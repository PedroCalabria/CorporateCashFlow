using CorporateCashFlow.Business.IBusiness;
using CorporateCashFlow.Entity;
using CorporateCashFlow.Repository.Imp.Context;
using Microsoft.EntityFrameworkCore;

namespace CorporateCashFlow.Repository.Imp.Repository;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserRefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _dbContext.UserRefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task<int> ConsumeAndRotateAsync(Guid oldTokenId, UserRefreshToken newToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var rowsAffected = await _dbContext.UserRefreshTokens
            .Where(t => t.Id == oldTokenId && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now));

        if (rowsAffected == 0)
        {
            await transaction.RollbackAsync();
            return 0;
        }

        await _dbContext.UserRefreshTokens.AddAsync(newToken);
        await _dbContext.SaveChangesAsync();

        await _dbContext.UserRefreshTokens
            .Where(t => t.Id == oldTokenId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.ReplacedByTokenId, newToken.Id));

        await transaction.CommitAsync();
        return rowsAffected;
    }

    public async Task RevokeAllByFamilyAsync(Guid familyId)
    {
        var now = DateTimeOffset.UtcNow;

        await _dbContext.UserRefreshTokens
            .Where(t => t.FamilyId == familyId && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now));
    }

    public async Task RevokeAllByUserIdAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;

        await _dbContext.UserRefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now));
    }

    public async Task AddAsync(UserRefreshToken token)
    {
        await _dbContext.UserRefreshTokens.AddAsync(token);
        await _dbContext.SaveChangesAsync();
    }
}
