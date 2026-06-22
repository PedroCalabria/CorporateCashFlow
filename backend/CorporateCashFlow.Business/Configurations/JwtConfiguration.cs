namespace CorporateCashFlow.Business.Configurations;

public class JwtConfiguration
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpiryHours { get; set; } = 8;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
