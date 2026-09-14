using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Records admin/employee actions worth being able to answer "who did this,
/// and when" about later - price/stock/role changes, deletions - the
/// accountability half of the role-based access control the rest of the
/// admin area enforces. Read-only after writing: nothing in the application
/// ever edits or deletes an entry.
/// </summary>
public interface IAuditLogStore
{
    void Record(int? actorUserId, string actorName, string action, string details);

    /// <summary>Most recent entries first - the Admin/AuditLog view.</summary>
    IReadOnlyList<AuditLogEntry> GetRecent(int count);
}
