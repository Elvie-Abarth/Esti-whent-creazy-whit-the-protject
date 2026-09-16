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

    /// <summary>Set to now at registration, and again on every login. Drives the
    /// 2-year inactivity auto-deletion policy - see InactiveAccountCleanupService.</summary>
    public DateTime LastActiveAt { get; init; }

    /// <summary>
    /// Base32 TOTP secret. Set as soon as enrollment starts (Account/Profile),
    /// before <see cref="TotpEnabled"/> is true - the pending secret has to
    /// survive a page refresh between scanning it into an authenticator app
    /// and entering the first code to confirm it actually works.
    /// </summary>
    public string? TotpSecret { get; init; }

    /// <summary>True only once enrollment is confirmed with a valid code - see LoginModel/VerifyTotpModel.</summary>
    public bool TotpEnabled { get; init; }
}
