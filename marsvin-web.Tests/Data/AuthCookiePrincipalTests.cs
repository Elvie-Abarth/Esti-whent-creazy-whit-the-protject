using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// The per-request check on the auth cookie, called directly: what ends a
/// session (a changed security stamp, the 8-hour limit, a cookie from
/// before either existed) and what merely refreshes it.
/// </summary>
[Collection("SqlCatalog collection")]
public class AuthCookiePrincipalTests(SqlCatalogFixture fixture)
{
    private sealed class ClockAt(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private ApplicationUser NewUser(UserRole role = UserRole.Customer)
    {
        var email = $"stamp-{Guid.NewGuid():N}@example.com";
        Assert.True(_users.CreateUser(email, "hash", "Stamp Test", role));
        return _users.FindByEmail(email)!;
    }

    private async Task<CookieValidatePrincipalContext> Revalidate(ClaimsPrincipal principal)
    {
        var services = new ServiceCollection()
            .AddSingleton<IUserAccountStore>(_users)
            .AddSingleton<TimeProvider>(new ClockAt(Now))
            .AddSingleton<IAuthenticationService>(new RecordingAuthenticationService())
            .BuildServiceProvider();
        var context = new CookieValidatePrincipalContext(
            new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme));

        await AuthCookiePrincipal.RevalidateAsync(context);
        return context;
    }

    [Fact]
    public async Task AFreshSession_IsLeftAlone()
    {
        var user = NewUser();

        var context = await Revalidate(AuthCookiePrincipal.Build(user, Now.AddHours(-1)));

        Assert.NotNull(context.Principal);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task ChangingThePassword_EndsASessionSignedInBeforeIt()
    {
        var user = NewUser();
        var cookieFromBefore = AuthCookiePrincipal.Build(user, Now.AddHours(-1));

        _users.UpdatePassword(user.UserId, "a-new-hash");

        Assert.Null((await Revalidate(cookieFromBefore)).Principal);
        // A session signed in after the change carries the new stamp and is fine.
        var after = AuthCookiePrincipal.Build(_users.FindById(user.UserId)!, Now);
        Assert.NotNull((await Revalidate(after)).Principal);
    }

    [Fact]
    public async Task LogOutEverywhere_EndsEverySessionOfThatAccount_AndNoOneElses()
    {
        var user = NewUser();
        var someoneElse = NewUser();
        var mine = AuthCookiePrincipal.Build(user, Now.AddHours(-1));
        var theirs = AuthCookiePrincipal.Build(someoneElse, Now.AddHours(-1));

        _users.RotateSecurityStamp(user.UserId);

        Assert.Null((await Revalidate(mine)).Principal);
        Assert.NotNull((await Revalidate(theirs)).Principal);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(9, false)]
    public async Task ASession_EndsAfter8Hours_HoweverActiveItHasBeen(int hoursSinceSignIn, bool stillValid)
    {
        var user = NewUser();

        var context = await Revalidate(AuthCookiePrincipal.Build(user, Now.AddHours(-hoursSinceSignIn)));

        Assert.Equal(stillValid, context.Principal is not null);
    }

    [Fact]
    public async Task ACookieFromBeforeStampsExisted_HasToLogInAgain()
    {
        var user = NewUser();
        var old = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme));

        Assert.Null((await Revalidate(old)).Principal);
    }

    [Fact]
    public async Task ARoleChange_RefreshesTheClaims_WithoutRestartingTheSessionsClock()
    {
        var user = NewUser(UserRole.Admin);
        var signedInAt = Now.AddHours(-6);
        var cookie = AuthCookiePrincipal.Build(user, signedInAt);

        _users.UpdateRole(user.UserId, UserRole.Employee);
        var context = await Revalidate(cookie);

        Assert.True(context.ShouldRenew);
        Assert.Equal("Employee", context.Principal!.FindFirstValue(ClaimTypes.Role));
        // Still 6 hours old - the demotion didn't buy another 8.
        Assert.Equal(signedInAt, AuthCookiePrincipal.SignedInAt(context.Principal));
    }

    [Fact]
    public void EveryAccount_GetsItsOwnStamp()
    {
        var first = NewUser();
        var second = NewUser();

        Assert.Equal(32, first.SecurityStamp.Length);
        Assert.NotEqual(first.SecurityStamp, second.SecurityStamp);
    }
}
