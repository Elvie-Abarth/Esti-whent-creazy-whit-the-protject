namespace MarsvinWebExample.Models;

/// <summary>A registered account - customer, employee, or admin.</summary>
public sealed class ApplicationUser
{
    public int UserId { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; init; }
    public UserRole Role { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; }
}
