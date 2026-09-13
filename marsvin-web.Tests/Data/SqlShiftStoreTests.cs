using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlShiftStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlShiftStore _shifts = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private ApplicationUser NewEmployee([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Employee);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void Create_ThenGetForUser_RoundTrips()
    {
        var staff = NewEmployee();
        var start = new DateTime(2026, 3, 5, 14, 0, 0);
        var end = new DateTime(2026, 3, 5, 18, 0, 0);

        _shifts.Create(staff.UserId, start, end, "Åbningsvagt");

        var found = Assert.Single(_shifts.GetForUser(staff.UserId));
        Assert.Equal(staff.UserId, found.UserId);
        Assert.Equal(staff.DisplayName, found.StaffName);
        Assert.Equal(start, found.StartAt);
        Assert.Equal(end, found.EndAt);
        Assert.Equal("Åbningsvagt", found.Note);
    }

    [Fact]
    public void Create_WithNoNote_RoundTripsAsNull()
    {
        var staff = NewEmployee();

        _shifts.Create(staff.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);

        var found = Assert.Single(_shifts.GetForUser(staff.UserId));
        Assert.Null(found.Note);
    }

    [Fact]
    public void GetForUser_NeverReturnsAnotherUsersShifts()
    {
        var staffA = NewEmployee();
        var staffB = NewEmployee();
        _shifts.Create(staffA.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        _shifts.Create(staffB.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);

        var found = _shifts.GetForUser(staffA.UserId);

        Assert.All(found, s => Assert.Equal(staffA.UserId, s.UserId));
    }

    [Fact]
    public void GetAll_IncludesShiftsFromEveryStaffMember()
    {
        var staffA = NewEmployee();
        var staffB = NewEmployee();
        _shifts.Create(staffA.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        _shifts.Create(staffB.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);

        var all = _shifts.GetAll();

        Assert.Contains(all, s => s.UserId == staffA.UserId);
        Assert.Contains(all, s => s.UserId == staffB.UserId);
    }

    [Fact]
    public void Delete_RemovesTheShift()
    {
        var staff = NewEmployee();
        _shifts.Create(staff.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var shift = Assert.Single(_shifts.GetForUser(staff.UserId));

        _shifts.Delete(shift.ShiftId);

        Assert.Empty(_shifts.GetForUser(staff.UserId));
    }

    [Fact]
    public void DeleteUser_AlsoRemovesTheirShifts()
    {
        var staff = NewEmployee();
        _shifts.Create(staff.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);

        // Would throw a FK violation if DeleteUser didn't clean up dbo.Shifts first.
        _users.DeleteUser(staff.UserId);

        Assert.Empty(_shifts.GetForUser(staff.UserId));
    }
}
