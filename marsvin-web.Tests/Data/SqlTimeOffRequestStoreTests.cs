using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlTimeOffRequestStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlTimeOffRequestStore _requests = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private ApplicationUser NewEmployee([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Employee);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void Create_ThenGetForUser_RoundTripsAsPending()
    {
        var staff = NewEmployee();

        _requests.Create(staff.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "Sommerferie");

        var found = Assert.Single(_requests.GetForUser(staff.UserId));
        Assert.Equal(staff.UserId, found.UserId);
        Assert.Equal(staff.DisplayName, found.StaffName);
        Assert.Equal(new DateOnly(2026, 6, 1), found.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 5), found.EndDate);
        Assert.Equal("Sommerferie", found.Reason);
        Assert.Equal(TimeOffStatus.Pending, found.Status);
        Assert.Null(found.DecidedAt);
        Assert.Null(found.DecidedByName);
    }

    [Fact]
    public void GetForUser_NeverReturnsAnotherUsersRequests()
    {
        var staffA = NewEmployee();
        var staffB = NewEmployee();
        _requests.Create(staffA.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);
        _requests.Create(staffB.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);

        var found = _requests.GetForUser(staffA.UserId);

        Assert.All(found, r => Assert.Equal(staffA.UserId, r.UserId));
    }

    [Fact]
    public void GetAll_IncludesRequestsFromEveryStaffMember()
    {
        var staffA = NewEmployee();
        var staffB = NewEmployee();
        _requests.Create(staffA.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);
        _requests.Create(staffB.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);

        var all = _requests.GetAll();

        Assert.Contains(all, r => r.UserId == staffA.UserId);
        Assert.Contains(all, r => r.UserId == staffB.UserId);
    }

    [Fact]
    public void Decide_Approve_SetsStatusDecidedAtAndDecidedByName()
    {
        var staff = NewEmployee();
        _requests.Create(staff.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);
        var request = Assert.Single(_requests.GetForUser(staff.UserId));

        _requests.Decide(request.RequestId, TimeOffStatus.Approved, "Butiksejer");

        var decided = Assert.Single(_requests.GetForUser(staff.UserId));
        Assert.Equal(TimeOffStatus.Approved, decided.Status);
        Assert.Equal("Butiksejer", decided.DecidedByName);
        Assert.NotNull(decided.DecidedAt);
    }

    [Fact]
    public void Decide_Deny_SetsStatusToDenied()
    {
        var staff = NewEmployee();
        _requests.Create(staff.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);
        var request = Assert.Single(_requests.GetForUser(staff.UserId));

        _requests.Decide(request.RequestId, TimeOffStatus.Denied, "Butiksejer");

        Assert.Equal(TimeOffStatus.Denied, Assert.Single(_requests.GetForUser(staff.UserId)).Status);
    }

    [Fact]
    public void DeleteUser_AlsoRemovesTheirTimeOffRequests()
    {
        var staff = NewEmployee();
        _requests.Create(staff.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null);

        // Would throw a FK violation if DeleteUser didn't clean up dbo.TimeOffRequests first.
        _users.DeleteUser(staff.UserId);

        Assert.Empty(_requests.GetForUser(staff.UserId));
    }
}
