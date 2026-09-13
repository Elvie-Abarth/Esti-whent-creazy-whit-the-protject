using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public interface ITimeOffRequestStore
{
    /// <summary>Just this one staff member's own requests, newest first - the Employee view.</summary>
    IReadOnlyList<TimeOffRequest> GetForUser(int userId);

    /// <summary>Every request across every staff member, newest first - the Admin view.</summary>
    IReadOnlyList<TimeOffRequest> GetAll();

    void Create(int userId, DateOnly startDate, DateOnly endDate, string? reason);

    void Decide(int requestId, TimeOffStatus status, string decidedByName);
}
