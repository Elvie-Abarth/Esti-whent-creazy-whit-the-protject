using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public interface IShiftStore
{
    /// <summary>Every shift, across every staff member, soonest first - the Admin view.</summary>
    IReadOnlyList<Shift> GetAll();

    /// <summary>Just this one staff member's own shifts - the Employee view.</summary>
    IReadOnlyList<Shift> GetForUser(int userId);

    /// <summary>Null if it doesn't exist - used to know who to notify before a shift is deleted.</summary>
    Shift? GetById(int shiftId);

    void Create(int userId, DateTime startAt, DateTime endAt, string? note);

    void Delete(int shiftId);
}
