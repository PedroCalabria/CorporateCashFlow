using System.Data;
using CorporateCashFlow.Business.IBusiness;
using CorporateCashFlow.Entity;
using Dapper;

namespace CorporateCashFlow.Repository.Imp.Repository;

public class UserRepository : IUserRepository
{
    private readonly IDbConnection _connection;

    public UserRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT Id, Name, Email, PasswordHash, Role, SubsidiaryId, IsActive, CreatedAt, UpdatedAt
            FROM Users
            WHERE Email = @email
            """;

        return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { email });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT Id, Name, Email, PasswordHash, Role, SubsidiaryId, IsActive, CreatedAt, UpdatedAt
            FROM Users
            WHERE Id = @id
            """;

        return await _connection.QuerySingleOrDefaultAsync<User>(sql, new { id });
    }
}
