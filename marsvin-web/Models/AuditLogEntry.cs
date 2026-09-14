namespace MarsvinWebExample.Models;

/// <summary>One recorded admin/employee action - see IAuditLogStore.</summary>
public sealed class AuditLogEntry
{
    public int AuditLogId { get; init; }

    /// <summary>Null once the acting account has been deleted - the entry itself is kept.</summary>
    public int? ActorUserId { get; init; }

    public required string ActorName { get; init; }
    public required string Action { get; init; }
    public required string Details { get; init; }
    public required DateTime CreatedAt { get; init; }
}
