namespace MarsvinWebExample.Models;

/// <summary>
/// One recorded sign of an attack *attempt* - a failed login, a locked
/// account, a wrong-role page request, a rate-limit hit, or a rejected
/// (forged or expired) form. Read by an admin on /Admin/SecurityLog.
///
/// This is deliberately NOT "proof of a break-in". No application can
/// honestly tell that it has been breached or that data has left it; what it
/// can see is the attempts and the suspicious patterns in its own logs. The
/// page and the alerts that use this are worded that way on purpose.
///
/// Only metadata is stored - never the attempted password, a token, or any
/// other secret. The attacker's IP is personal data, so these rows are
/// pruned after a retention window (see InactiveAccountCleanupService) and
/// the practice is noted on /Privatliv.
/// </summary>
public sealed class SecurityEvent
{
    public long SecurityEventId { get; init; }
    public DateTime CreatedAt { get; init; }
    public SecurityEventType Type { get; init; }

    /// <summary>The email the attempt was aimed at, normalised - null when there is none (a 429, a wrong-role click).</summary>
    public string? Email { get; init; }

    /// <summary>The client IP, or null when it couldn't be read.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The path that was being requested.</summary>
    public string? Path { get; init; }

    /// <summary>A short, already-sanitised note - never a secret.</summary>
    public string? Detail { get; init; }
}

public enum SecurityEventType : byte
{
    LoginFailed = 0,
    AccountLocked = 1,
    AccessDenied = 2,
    RateLimited = 3,
    AntiforgeryRejected = 4
}

/// <summary>
/// How much an event is worth an admin's immediate attention, so the security
/// log can be colour-coded. It is derived from the type (not stored), so it
/// stays a single source of truth. The reasoning:
///   Low    - a single failed login is almost always a typo.
///   Medium - rate-limiting (automated flooding) or a rejected form (often
///            just an expired session, sometimes a forged request).
///   High   - an account locked by repeated failures (a brute-force attempt
///            in progress), or a signed-in user reaching for a page their
///            role forbids (privilege probing / forced browsing).
/// </summary>
public enum SecurityEventSeverity : byte
{
    Low = 0,
    Medium = 1,
    High = 2
}

public static class SecurityEventTypeExtensions
{
    public static string DisplayName(this SecurityEventType type, bool english = false) => type switch
    {
        SecurityEventType.LoginFailed => english ? "Failed login" : "Mislykket login",
        SecurityEventType.AccountLocked => english ? "Account locked" : "Konto låst",
        SecurityEventType.AccessDenied => english ? "Access denied (wrong role)" : "Adgang nægtet (forkert rolle)",
        SecurityEventType.RateLimited => english ? "Rate limited" : "For mange forsøg",
        SecurityEventType.AntiforgeryRejected => english ? "Rejected form (CSRF/expired)" : "Afvist formular (CSRF/udløbet)",
        _ => type.ToString()
    };

    public static SecurityEventSeverity Severity(this SecurityEventType type) => type switch
    {
        SecurityEventType.LoginFailed => SecurityEventSeverity.Low,
        SecurityEventType.RateLimited => SecurityEventSeverity.Medium,
        SecurityEventType.AntiforgeryRejected => SecurityEventSeverity.Medium,
        SecurityEventType.AccountLocked => SecurityEventSeverity.High,
        SecurityEventType.AccessDenied => SecurityEventSeverity.High,
        _ => SecurityEventSeverity.Medium
    };

    public static string DisplayName(this SecurityEventSeverity severity, bool english = false) => severity switch
    {
        SecurityEventSeverity.Low => english ? "Low" : "Lav",
        SecurityEventSeverity.Medium => english ? "Medium" : "Middel",
        SecurityEventSeverity.High => english ? "High" : "Høj",
        _ => severity.ToString()
    };

    /// <summary>The CSS modifier suffix - "low", "medium" or "high" - for the badge and row classes.</summary>
    public static string CssName(this SecurityEventSeverity severity) => severity.ToString().ToLowerInvariant();
}
