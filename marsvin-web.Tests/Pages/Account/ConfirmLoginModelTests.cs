using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Pages.Account;

[Collection("SqlCatalog collection")]
public class ConfirmLoginModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);

    private (ConfirmLoginModel Model, RecordingAuthenticationService Auth) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var model = new ConfirmLoginModel(_users, _pendingLogins) { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth);
    }

    private ApplicationUser NewCustomer([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, "whatever"), caller, UserRole.Customer);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_SignsInAndRedirectsHome()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (model, auth) = MakeModel();

        var result = await model.OnGetAsync(token);

        Assert.NotNull(auth.SignedInAs);
        Assert.Equal(user.UserId.ToString(), auth.SignedInAs!.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_RecordsActivity()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (model, _) = MakeModel();

        await model.OnGetAsync(token);

        var refreshed = _users.FindById(user.UserId)!;
        Assert.True(refreshed.LastActiveAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task OnGetAsync_ValidToken_WithReturnUrl_RedirectsThereInstead()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: "/Marsvin", TimeSpan.FromMinutes(15));
        var (model, _) = MakeModel();

        var result = await model.OnGetAsync(token);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Marsvin", redirect.Url);
    }

    [Fact]
    public async Task OnGetAsync_TokenIsSingleUse_SecondAttemptFails()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var (first, _) = MakeModel();
        await first.OnGetAsync(token);

        var (second, auth2) = MakeModel();
        var result = await second.OnGetAsync(token);

        Assert.Null(auth2.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ExpiredToken_DoesNotSignIn()
    {
        var user = NewCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        var (model, auth) = MakeModel();

        var result = await model.OnGetAsync(token);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_UnknownToken_DoesNotSignIn()
    {
        var (model, auth) = MakeModel();

        var result = await model.OnGetAsync("not-a-real-token");

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_NullToken_DoesNotSignIn()
    {
        var (model, auth) = MakeModel();

        var result = await model.OnGetAsync(null);

        Assert.Null(auth.SignedInAs);
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_DeactivatedAccount_DoesNotSignIn()
    {
        var user = NewCustomer();
        _users.SetActive(user.UserId, false);
        try
        {
            var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
            var (model, auth) = MakeModel();

            var result = await model.OnGetAsync(token);

            Assert.Null(auth.SignedInAs);
            Assert.IsType<PageResult>(result);
        }
        finally
        {
            _users.SetActive(user.UserId, true);
        }
    }
}
