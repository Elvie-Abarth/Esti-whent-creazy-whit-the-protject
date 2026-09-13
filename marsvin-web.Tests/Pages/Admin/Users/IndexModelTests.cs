using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Admin.Users;
using MarsvinWebExample.Tests.Data;

namespace MarsvinWebExample.Tests.Pages.Admin.Users;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private IndexModel MakeModelSignedInAs(ApplicationUser signedInUser)
    {
        var model = new IndexModel(_users) { PageContext = TestAuth.ContextFor(signedInUser.UserId, signedInUser.Role.ToString()) };
        return model;
    }

    // Deactivates every other active admin (including the seeded
    // admin@marsvin.dk) so `keepActive` becomes the only one - callers must
    // reactivate the returned list afterward, since this is a shared fixture
    // database other tests in the same run also depend on having their admins intact.
    private List<ApplicationUser> IsolateAsOnlyActiveAdmin(int keepActiveUserId) =>
        _users.GetAll()
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.UserId != keepActiveUserId)
            .Select(u => { _users.SetActive(u.UserId, false); return u; })
            .ToList();

    private void Reactivate(IEnumerable<ApplicationUser> admins)
    {
        foreach (var admin in admins) _users.SetActive(admin.UserId, true);
    }

    private ApplicationUser NewAdmin([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Admin);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void OnPostUpdateRole_OnOwnAccount_IsBlockedAndRoleUnchanged()
    {
        var admin = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostUpdateRole(admin.UserId, UserRole.Customer);

        Assert.Equal(UserRole.Admin, _users.FindById(admin.UserId)!.Role);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostUpdateRole_OnAnotherAccount_Succeeds()
    {
        var admin = NewAdmin();
        var other = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostUpdateRole(other.UserId, UserRole.Employee);

        Assert.Equal(UserRole.Employee, _users.FindById(other.UserId)!.Role);
    }

    [Fact]
    public void OnPostToggleActive_OnOwnAccount_IsBlockedAndStaysActive()
    {
        var admin = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostToggleActive(admin.UserId, false);

        Assert.True(_users.FindById(admin.UserId)!.IsActive);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostToggleActive_OnAnotherAccount_Succeeds()
    {
        var admin = NewAdmin();
        var other = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostToggleActive(other.UserId, false);

        Assert.False(_users.FindById(other.UserId)!.IsActive);
    }

    [Fact]
    public void OnPostDelete_OnOwnAccount_IsBlockedAndAccountRemains()
    {
        var admin = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostDelete(admin.UserId);

        Assert.NotNull(_users.FindById(admin.UserId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostDelete_OnAnotherAccount_RemovesIt()
    {
        var admin = NewAdmin();
        var other = NewAdmin();
        var model = MakeModelSignedInAs(admin);

        model.OnPostDelete(other.UserId);

        Assert.Null(_users.FindById(other.UserId));
    }

    [Fact]
    public void OnPostDelete_LastActiveAdmin_IsBlockedAndAccountRemains()
    {
        var lastAdmin = NewAdmin();
        var deactivated = IsolateAsOnlyActiveAdmin(lastAdmin.UserId);
        try
        {
            Assert.Equal(1, _users.CountActiveAdmins());
            // Acting as a different id than lastAdmin so self-protection
            // doesn't fire instead - only IsLastActiveAdmin's own logic is
            // under test here ([Authorize(Roles="Admin")] on the real page
            // is enforced by the framework, not this method call).
            var model = new IndexModel(_users) { PageContext = TestAuth.ContextFor(0, "Admin") };

            model.OnPostDelete(lastAdmin.UserId);

            Assert.NotNull(_users.FindById(lastAdmin.UserId));
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            Reactivate(deactivated);
        }
    }

    [Fact]
    public void OnPostUpdateRole_LastActiveAdmin_IsBlockedAndRoleUnchanged()
    {
        var lastAdmin = NewAdmin();
        var deactivated = IsolateAsOnlyActiveAdmin(lastAdmin.UserId);
        try
        {
            var model = new IndexModel(_users) { PageContext = TestAuth.ContextFor(0, "Admin") };

            model.OnPostUpdateRole(lastAdmin.UserId, UserRole.Employee);

            Assert.Equal(UserRole.Admin, _users.FindById(lastAdmin.UserId)!.Role);
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            Reactivate(deactivated);
        }
    }

    [Fact]
    public void OnPostUpdateRole_LastActiveAdmin_StayingAdmin_IsAllowed()
    {
        // Not actually a role change (Admin -> Admin), so there's nothing for
        // the last-admin guard to object to.
        var lastAdmin = NewAdmin();
        var deactivated = IsolateAsOnlyActiveAdmin(lastAdmin.UserId);
        try
        {
            var model = new IndexModel(_users) { PageContext = TestAuth.ContextFor(0, "Admin") };

            model.OnPostUpdateRole(lastAdmin.UserId, UserRole.Admin);

            Assert.Null(model.ErrorMessage);
        }
        finally
        {
            Reactivate(deactivated);
        }
    }

    [Fact]
    public void OnPostToggleActive_LastActiveAdmin_DeactivationIsBlocked()
    {
        var lastAdmin = NewAdmin();
        var deactivated = IsolateAsOnlyActiveAdmin(lastAdmin.UserId);
        try
        {
            var model = new IndexModel(_users) { PageContext = TestAuth.ContextFor(0, "Admin") };

            model.OnPostToggleActive(lastAdmin.UserId, false);

            Assert.True(_users.FindById(lastAdmin.UserId)!.IsActive);
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            Reactivate(deactivated);
        }
    }
}
