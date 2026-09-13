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
}
