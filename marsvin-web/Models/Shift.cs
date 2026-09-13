namespace MarsvinWebExample.Models;

/// <summary>A scheduled work shift for one staff member (Employee or Admin).</summary>
public sealed class Shift
{
    public int ShiftId { get; init; }
    public required int UserId { get; init; }
    public required string StaffName { get; init; }
    public required DateTime StartAt { get; init; }
    public required DateTime EndAt { get; init; }
    public string? Note { get; init; }
}
