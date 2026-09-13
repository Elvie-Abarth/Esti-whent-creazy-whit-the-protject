namespace MarsvinWebExample.Models;

/// <summary>A day-off request an Employee submits and an Admin approves or denies.</summary>
public sealed class TimeOffRequest
{
    public int RequestId { get; init; }
    public required int UserId { get; init; }
    public required string StaffName { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public string? Reason { get; init; }
    public required TimeOffStatus Status { get; init; }
    public required DateTime RequestedAt { get; init; }
    public DateTime? DecidedAt { get; init; }

    // A snapshot of the deciding admin's name, not a foreign key - survives
    // that admin's account later being deleted, the same way OrderItems
    // keeps a snapshot of ProductName rather than pointing at a Product that
    // might not exist anymore.
    public string? DecidedByName { get; init; }
}
