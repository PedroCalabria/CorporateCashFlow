using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CorporateCashFlow.Business.Configurations;
using CorporateCashFlow.Business.Exceptions;
using CorporateCashFlow.Business.IBusiness;
using CorporateCashFlow.Entity;
using CorporateCashFlow.Entity.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CorporateCashFlow.Business.Imp.Business;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ISecurityAuditRepository _securityAuditRepository;
    private readonly JwtConfiguration _jwtConfiguration;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ISecurityAuditRepository securityAuditRepository,
        IOptions<JwtConfiguration> jwtConfiguration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _securityAuditRepository = securityAuditRepository;
        _jwtConfiguration = jwtConfiguration.Value;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string ipAddress)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            await _securityAuditRepository.LogAsync(null, "Login.Failure", "Failure", ipAddress, "Invalid credentials");
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _securityAuditRepository.LogAsync(user.Id, "Login.Failure", "Failure", ipAddress, "Invalid credentials");
            throw new UnauthorizedException("Invalid email or password.");
        }

        var jti = Guid.NewGuid().ToString();
        var (accessToken, expiresAt) = GenerateAccessToken(user, jti);
        var rawRefreshToken = Guid.NewGuid().ToString("N");
        var familyId = Guid.NewGuid();

        var refreshTokenEntity = new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            AccessTokenJti = jti,
            FamilyId = familyId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtConfiguration.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity);
        await _securityAuditRepository.LogAsync(user.Id, "Login.Success", "Success", ipAddress, null);

        return new LoginResponseDto
        {
            Token = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = expiresAt
        };
    }

    public async Task<UserContextResponseDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("The provided token is invalid.");
        }

        return new UserContextResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            SubsidiaryId = user.SubsidiaryId
        };
    }

    public async Task<TokenRefreshResponseDto> RefreshTokenAsync(TokenRefreshRequestDto request, string ipAddress)
    {
        try
        {
            var tokenHash = HashToken(request.RefreshToken);
            var record = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

            if (record is null)
            {
                await _securityAuditRepository.LogAsync(null, "Refresh.Failure.Expired", "Failure", ipAddress, "Token not found");
                throw new UnauthorizedException("The refresh token has expired.");
            }

            if (record.IsRevoked)
            {
                await _securityAuditRepository.LogAsync(record.UserId, "Refresh.Failure.Replay", "Failure", ipAddress, "Replay detected");
                await _refreshTokenRepository.RevokeAllByFamilyAsync(record.FamilyId);
                throw new UnauthorizedException("The refresh token has already been used or revoked.");
            }

            if (record.ExpiresAt < DateTimeOffset.UtcNow)
            {
                await _securityAuditRepository.LogAsync(record.UserId, "Refresh.Failure.Expired", "Failure", ipAddress, "Token expired");
                throw new UnauthorizedException("The refresh token has expired.");
            }

            var jti = ExtractJtiWithoutLifetimeValidation(request.AccessToken);
            if (jti is null || jti != record.AccessTokenJti)
            {
                throw new UnauthorizedException("The refresh token has already been used or revoked.");
            }

            var newJti = Guid.NewGuid().ToString();
            var rawNewRefreshToken = Guid.NewGuid().ToString("N");
            var newTokenEntity = new UserRefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = record.UserId,
                TokenHash = HashToken(rawNewRefreshToken),
                AccessTokenJti = newJti,
                FamilyId = record.FamilyId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtConfiguration.RefreshTokenExpiryDays),
                IsRevoked = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var rowsAffected = await _refreshTokenRepository.ConsumeAndRotateAsync(record.Id, newTokenEntity);
            if (rowsAffected == 0)
            {
                await _securityAuditRepository.LogAsync(record.UserId, "Refresh.Failure.Race", "Failure", ipAddress, "Optimistic lock lost");
                throw new UnauthorizedException("The refresh token has already been used or revoked.");
            }

            var (accessToken, expiresAt) = GenerateAccessToken(record.User, newJti);
            await _securityAuditRepository.LogAsync(record.UserId, "Refresh.Success", "Success", ipAddress, null);

            return new TokenRefreshResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = rawNewRefreshToken,
                ExpiresAt = expiresAt
            };
        }
        catch (UnauthorizedException)
        {
            throw;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException)
        {
            await _securityAuditRepository.LogAsync(null, "Refresh.Failure.Unavailable", "Failure", ipAddress, ex.Message);
            throw new ServiceUnavailableException("Token validation service temporarily unavailable");
        }
    }

    public async Task LogoutAsync(Guid userId, string ipAddress)
    {
        await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);
        await _securityAuditRepository.LogAsync(userId, "Logout.Success", "Success", ipAddress, null);
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user, string jti)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_jwtConfiguration.AccessTokenExpiryHours);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfiguration.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("role", user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti)
        };

        if (user.SubsidiaryId.HasValue)
        {
            claims.Add(new Claim("subsidiary_id", user.SubsidiaryId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtConfiguration.Issuer,
            audience: _jwtConfiguration.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private string? ExtractJtiWithoutLifetimeValidation(string accessToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(accessToken))
        {
            return null;
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtConfiguration.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtConfiguration.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfiguration.Secret)),
            ValidateLifetime = false
        };

        try
        {
            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
            return principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
