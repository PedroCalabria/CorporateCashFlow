namespace CorporateCashFlow.Entity.DTOs;

public class UserContextResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? SubsidiaryId { get; set; }
}
