namespace CorporateCashFlow.Entity.DTOs;

public class TokenRefreshRequestDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string AccessToken { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string RefreshToken { get; set; } = string.Empty;
}
