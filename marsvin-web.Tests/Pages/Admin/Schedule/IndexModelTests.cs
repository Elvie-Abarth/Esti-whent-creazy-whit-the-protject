using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Admin.Schedule;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages.Admin.Schedule;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlShiftStore _shifts = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private IndexModel MakeModelSignedInAs(ApplicationUser signedInUser) =>
        new(_shifts, _users) { PageContext = TestAuth.ContextFor(signedInUser.UserId, signedInUser.Role.ToString()) };

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
    public void OnPostCreate_Admin_AddsTheShift()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        model.OnPostCreate(employee.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), "Test");

        Assert.Contains(_shifts.GetForUser(employee.UserId), s => s.Note == "Test");
    }

    [Fact]
    public void OnPostCreate_Employee_IsForbiddenAndCreatesNothing()
    {
        var employee = NewStaff(UserRole.Employee);
        var other = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(employee);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var result = model.OnPostCreate(other.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_shifts.GetForUser(other.UserId));
    }

    [Fact]
    public void OnPostCreate_EndTimeNotAfterStartTime_ShowsErrorAndCreatesNothing()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        model.OnPostCreate(employee.UserId, date, new TimeOnly(18, 0), new TimeOnly(14, 0), null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Empty(_shifts.GetForUser(employee.UserId));
    }

    [Fact]
    public void OnPostCreate_TargetIsACustomer_ShowsErrorAndCreatesNothing()
    {
        var admin = NewStaff(UserRole.Admin);
        var customer = NewStaff(UserRole.Customer);
        var model = MakeModelSignedInAs(admin);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        model.OnPostCreate(customer.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Empty(_shifts.GetForUser(customer.UserId));
    }

    [Fact]
    public void OnPostDelete_Admin_RemovesTheShift()
    {
        var admin = NewStaff(UserRole.Admin);
        var employee = NewStaff(UserRole.Employee);
        _shifts.Create(employee.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var shift = Assert.Single(_shifts.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(admin);

        model.OnPostDelete(shift.ShiftId);

        Assert.Empty(_shifts.GetForUser(employee.UserId));
    }

    [Fact]
    public void OnPostDelete_Employee_IsForbiddenAndShiftRemains()
    {
        var employee = NewStaff(UserRole.Employee);
        _shifts.Create(employee.UserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(4), null);
        var shift = Assert.Single(_shifts.GetForUser(employee.UserId));
        var model = MakeModelSignedInAs(employee);

        var result = model.OnPostDelete(shift.ShiftId);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(_shifts.GetForUser(employee.UserId));
    }
}
