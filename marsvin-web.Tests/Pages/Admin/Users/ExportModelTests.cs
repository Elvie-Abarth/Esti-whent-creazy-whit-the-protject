using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Admin.Users;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Admin.Users;

[Collection("SqlCatalog collection")]
public class ExportModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    [Fact]
    public void OnGet_KnownId_SetsAccountAndReturnsPage()
    {
        var email = $"export-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Export Me", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        var model = new ExportModel(_users);

        var result = model.OnGet(user.UserId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(email, model.Account.Email);
    }

    [Fact]
    public void OnGet_UnknownId_ReturnsNotFound()
    {
        var model = new ExportModel(_users);

        var result = model.OnGet(999999);

        Assert.IsType<NotFoundResult>(result);
    }
}
