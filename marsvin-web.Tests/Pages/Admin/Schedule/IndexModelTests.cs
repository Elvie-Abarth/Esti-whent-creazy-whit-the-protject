using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Admin.Schedule;
using MarsvinWebExample.Tests.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages.Admin.Schedule;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlShiftStore _shifts = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlTimeOffRequestStore _timeOffRequests = new(fixture.ConnectionString);

    // Shared across every model this test creates - a single test may sign in
    // as more than one user in turn, and still wants to see everything sent.
    private readonly RecordingEmailSender _email = new();

    private IndexModel MakeModelSignedInAs(ApplicationUser signedInUser) =>
        new(_shifts, _users, _timeOffRequests, _email) { PageContext = TestAuth.ContextFor(signedInUser.UserId, signedInUser.Role.ToString()) };

    private ApplicationUser NewStaff(UserRole role, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, role);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void OnGet_Admin_SeesEveryStaffMembersShiftsAndTheStaffList()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _shifts.Create(employee.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var model = MakeModelSignedInAs(admin);

        model.OnGet();

        Assert.Contains(model.Shifts, s => s.UserId == employee.UserId);
        Assert.Contains(model.StaffMembers, u => u.UserId == employee.UserId);
    }

    [Fact]
    public void OnGet_Employee_SeesOnlyTheirOwnShiftsAndNoStaffList()
    {
        var employeeA = NewStaff(UserRole.Employee);
        var employeeB = NewStaff(UserRole.Employee);
        _shifts.Create(employeeA.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        _shifts.Create(employeeB.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var model = MakeModelSignedInAs(employeeA);

        model.OnGet();

        Assert.All(model.Shifts, s => Assert.Equal(employeeA.UserId, s.UserId));
        Assert.Empty(model.StaffMembers);
    }

    [Fact]
    public async Task OnPostCreateAsync_Admin_AddsTheShiftAndEmailsTheStaffMember()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        await model.OnPostCreateAsync(employee.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), "Test");

        Assert.Contains(_shifts.GetForUser(employee.UserId), s => s.Note == "Test");
        Assert.Contains(_email.Sent, e => e.ToEmail == employee.Email);
    }

    [Fact]
    public async Task OnPostCreateAsync_Employee_IsForbiddenAndCreatesNothing()
    {
        var employee = NewStaff(UserRole.Employee);
        var other = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(employee);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var result = await model.OnPostCreateAsync(other.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_shifts.GetForUser(other.UserId));
    }

    [Fact]
    public async Task OnPostCreateAsync_EndTimeNotAfterStartTime_ShowsErrorAndCreatesNothing()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        await model.OnPostCreateAsync(employee.UserId, date, new TimeOnly(18, 0), new TimeOnly(14, 0), null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Empty(_shifts.GetForUser(employee.UserId));
    }

    [Fact]
    public async Task OnPostCreateAsync_TargetIsACustomer_ShowsErrorAndCreatesNothing()
    {
        var admin = NewStaff(UserRole.Admin);
        var customer = NewStaff(UserRole.Customer);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        await model.OnPostCreateAsync(customer.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Empty(_shifts.GetForUser(customer.UserId));
    }

    [Fact]
    public async Task OnPostDeleteAsync_Admin_RemovesTheShiftAndEmailsTheStaffMember()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _shifts.Create(employee.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var shift = Assert.Single(_shifts.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(admin);

        await model.OnPostDeleteAsync(shift.ShiftId);

        Assert.Empty(_shifts.GetForUser(employee.UserId));
        Assert.Contains(_email.Sent, e => e.ToEmail == employee.Email);
    }

    [Fact]
    public async Task OnPostDeleteAsync_Employee_IsForbiddenAndShiftRemains()
    {
        var employee = NewStaff(UserRole.Employee);
        _shifts.Create(employee.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var shift = Assert.Single(_shifts.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(employee);

        var result = await model.OnPostDeleteAsync(shift.ShiftId);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(_shifts.GetForUser(employee.UserId));
    }

    [Fact]
    public void OnGet_Admin_SeesOnlyStillPendingRequests()
    {
        // Other tests in this shared fixture database leave their own pending
        // rows behind too, so this checks by content rather than assuming
        // PendingRequests contains exactly one item overall.
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(employee.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), "Pending one");
        _timeOffRequests.Create(employee.UserId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3), "Decided one");
        var decided = _timeOffRequests.GetForUser(employee.UserId).Single(r => r.Reason == "Decided one");
        _timeOffRequests.Decide(decided.RequestId, TimeOffStatus.Approved, admin.DisplayName);
        var model = MakeModelSignedInAs(admin);

        model.OnGet();

        Assert.Contains(model.PendingRequests, r => r.UserId == employee.UserId && r.Reason == "Pending one");
        Assert.DoesNotContain(model.PendingRequests, r => r.UserId == employee.UserId && r.Reason == "Decided one");
    }

    [Fact]
    public void OnGet_Employee_NeverSeesPendingRequestsList()
    {
        var employee = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(employee.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), null);
        var model = MakeModelSignedInAs(employee);

        model.OnGet();

        Assert.Empty(model.PendingRequests);
    }

    [Fact]
    public void OnPostDecideRequest_Admin_Approve_UpdatesStatusAndDecidedBy()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(employee.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), null);
        var request = Assert.Single(_timeOffRequests.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(admin);

        model.OnPostDecideRequest(request.RequestId, approve: true);

        var decided = Assert.Single(_timeOffRequests.GetForUser(employee.UserId));
        Assert.Equal(TimeOffStatus.Approved, decided.Status);
        Assert.Equal(admin.DisplayName, decided.DecidedByName);
        Assert.NotNull(decided.DecidedAt);
    }

    [Fact]
    public void OnPostDecideRequest_Admin_Deny_UpdatesStatus()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(employee.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), null);
        var request = Assert.Single(_timeOffRequests.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(admin);

        model.OnPostDecideRequest(request.RequestId, approve: false);

        var decided = Assert.Single(_timeOffRequests.GetForUser(employee.UserId));
        Assert.Equal(TimeOffStatus.Denied, decided.Status);
    }

    [Fact]
    public void OnPostDecideRequest_Employee_IsForbiddenAndRequestStaysPending()
    {
        var employeeA = NewStaff(UserRole.Employee);
        var employeeB = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(employeeA.UserId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), null);
        var request = Assert.Single(_timeOffRequests.GetForUser(employeeA.UserId));
        var model = MakeModelSignedInAs(employeeB);

        var result = model.OnPostDecideRequest(request.RequestId, approve: true);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(TimeOffStatus.Pending, Assert.Single(_timeOffRequests.GetForUser(employeeA.UserId)).Status);
    }
}
