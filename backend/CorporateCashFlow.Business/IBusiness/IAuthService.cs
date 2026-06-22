using CorporateCashFlow.Entity.DTOs;

namespace CorporateCashFlow.Business.IBusiness;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string ipAddress);
    Task<UserContextResponseDto> GetCurrentUserAsync(Guid userId);
    Task<TokenRefreshResponseDto> RefreshTokenAsync(TokenRefreshRequestDto request, string ipAddress);
    Task LogoutAsync(Guid userId, string ipAddress);
}
