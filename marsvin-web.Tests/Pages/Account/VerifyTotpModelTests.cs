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
public class VerifyTotpModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);
    private readonly LoginLockoutTracker _lockout = new();

    private (VerifyTotpModel Model, RecordingAuthenticationService Auth) MakeModel()
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var model = new VerifyTotpModel(_users, _pendingLogins, _lockout)
            { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth);
    }

    private (ApplicationUser User, string Secret) NewTotpEnrolledCustomer(
        [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, "CorrectPass123!"), caller, UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        var secret = Totp.GenerateSecret();
        _users.SetTotpSecret(user.UserId, secret);
        _users.SetTotpEnabled(user.UserId, true);
        return (_users.FindById(user.UserId)!, secret);
    }

    [Fact]
    public void OnGet_InvalidToken_RedirectsToLogin()
    {
        var (model, _) = MakeModel();

        var result = model.OnGet("not-a-real-token");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Login", redirect.PageName);
    }

    [Fact]
    public void OnGet_ValidToken_ShowsThePage()
    {
        var (user, _) = NewTotpEnrolledCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(5));
        var (model, _) = MakeModel();

        var result = model.OnGet(token);

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_CorrectCode_SignsInAndConsumesToken()
    {
        var (user, secret) = NewTotpEnrolledCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(5));
        var (model, auth) = MakeModel();
        model.Token = token;
        model.Code = TotpTestHelper.CurrentCode(secret);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(auth.SignedInAs);
        Assert.False(_pendingLogins.IsValid(token)); // single-use
    }

    [Fact]
    public async Task OnPostAsync_WrongCode_DoesNotSignInAndTokenStaysValidForRetry()
    {
        var (user, _) = NewTotpEnrolledCustomer();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(5));
        var (model, auth) = MakeModel();
        model.Token = token;
        model.Code = "000000";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.True(_pendingLogins.IsValid(token)); // not burned by a wrong attempt
    }

    [Fact]
    public async Task OnPostAsync_FiveWrongCodes_LocksOutTheAccount()
    {
        var (user, secret) = NewTotpEnrolledCustomer();

        for (var i = 0; i < 5; i++)
        {
            var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(5));
            var (attempt, _) = MakeModel();
            attempt.Token = token;
            attempt.Code = "000000";
            await attempt.OnPostAsync();
        }

        var finalToken = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(5));
        var (finalTry, auth) = MakeModel();
        finalTry.Token = finalToken;
        finalTry.Code = TotpTestHelper.CurrentCode(secret); // even the *correct* code, now locked out

        var result = await finalTry.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
    }

    [Fact]
    public async Task OnPostAsync_ExpiredToken_ShowsErrorWithoutThrowing()
    {
        var (model, auth) = MakeModel();
        model.Token = "not-a-real-token";
        model.Code = "123456";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.False(model.ModelState.IsValid);
    }
}
