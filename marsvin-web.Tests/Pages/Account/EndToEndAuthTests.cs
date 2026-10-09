using System.Net;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Pages.Account;

/// <summary>
/// Drives the real app over HTTP (routing, authentication, authorization,
/// and - the part unit tests calling PageModel methods directly can't see -
/// antiforgery/CSRF validation and actual cookie behavior).
/// </summary>
[Collection("WebApp collection")]
public class EndToEndAuthTests(MarsvinWebAppFactory factory)
{
    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    [Fact]
    public async Task Get_RegisterPage_ReturnsAntiforgeryTokenAndCookie()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        var (response, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.Contains(".AspNetCore.Antiforgery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Post_Register_WithoutAntiforgeryToken_IsRejected()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register"); // establishes the antiforgery cookie

        var email = $"no-token-{Guid.NewGuid():N}@example.com";
        var response = await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            // Deliberately no __RequestVerificationToken field.
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "No Token",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Register_WithTokenFromADifferentSession_IsRejected()
    {
        // The antiforgery cookie and the form token are a matched pair tied
        // to one session. Taking a valid-looking token from session B and
        // presenting it alongside session A's cookie must fail - otherwise
        // the token would protect nothing.
        var client = MakeClient();

        var jarA = new CookieJar();
        await HttpTestHelpers.GetWithToken(client, jarA, "/Account/Register");

        var jarB = new CookieJar();
        var (_, _, tokenFromSessionB) = await HttpTestHelpers.GetWithToken(client, jarB, "/Account/Register");

        var email = $"mismatched-{Guid.NewGuid():N}@example.com";
        var response = await HttpTestHelpers.PostForm(client, jarA, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = tokenFromSessionB,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Mismatched",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_RegisterThenAccessProtectedPageThenLogoutBlocksAccessAgain()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = $"fullflow-{Guid.NewGuid():N}@example.com";

        // 1. Register - creates a fresh Customer account, but doesn't sign in
        // yet (see RegisterModelTests) - it redirects to CheckEmail instead.
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        var registerResponse = await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Full Flow",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        Assert.Equal(HttpStatusCode.Found, registerResponse.StatusCode);
        Assert.Equal("/Account/CheckEmail?purpose=register", registerResponse.Headers.Location!.ToString());

        // 1b. Open the confirmation link (see HttpTestHelpers.CompleteEmailConfirmation)
        // - this is the step that actually signs the session in.
        var confirmResponse = await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
        Assert.True(confirmResponse.Headers.TryGetValues("Set-Cookie", out var authCookies));
        var authCookieHeader = Assert.Single(authCookies!, c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.Contains("httponly", authCookieHeader, StringComparison.OrdinalIgnoreCase);

        // 2. Now authenticated - a page requiring login should be reachable.
        // Profile, not Cart/Index - the cart allows guests too (see
        // EndToEndCartTests), so it's no longer a useful "protected page"
        // example for this test's purpose.
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        jar.Apply(profileRequest);
        var profileResponse = await client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        // 3. Log out (with a valid antiforgery token from that same page).
        var profileHtml = await profileResponse.Content.ReadAsStringAsync();
        var logoutToken = CookieJar.ExtractAntiforgeryToken(profileHtml);
        var logoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Account/Logout", new()
        {
            ["__RequestVerificationToken"] = logoutToken
        });
        Assert.Equal(HttpStatusCode.Found, logoutResponse.StatusCode);
        Assert.True(logoutResponse.Headers.TryGetValues("Set-Cookie", out var clearingCookies));
        var clearedAuthCookie = Assert.Single(clearingCookies!, c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.Contains("1970", clearedAuthCookie); // expired in the past - browsers delete it

        // 4. The auth cookie value is now cleared - the same protected page must redirect to login again.
        var afterLogoutRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        jar.Apply(afterLogoutRequest);
        var afterLogoutResponse = await client.SendAsync(afterLogoutRequest);
        Assert.Equal(HttpStatusCode.Found, afterLogoutResponse.StatusCode);
        Assert.Contains("/Account/Login", afterLogoutResponse.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_Logout_WithoutAntiforgeryToken_IsRejectedAndSessionStaysLoggedIn()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = $"logout-no-token-{Guid.NewGuid():N}@example.com";

        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Logout Guard",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);

        var logoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Account/Logout", new());
        Assert.Equal(HttpStatusCode.BadRequest, logoutResponse.StatusCode);

        // Still logged in - the rejected logout must not have signed the session out.
        var cartRequest = new HttpRequestMessage(HttpMethod.Get, "/Cart/Index");
        jar.Apply(cartRequest);
        var cartResponse = await client.SendAsync(cartRequest);
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedPage_RedirectsToLogin()
    {
        var client = MakeClient();

        var response = await client.GetAsync("/Admin/Index");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SignedInEmployee_PostingToAnAdminOnlyHandler_IsRedirectedToAccessDenied()
    {
        // Admin/Schedule/Index is shared by both roles at the page level
        // ([Authorize(Roles = "Admin,Employee")]), but DecideRequest is
        // Admin-only, checked (and Forbid()-returned) inside the handler
        // itself. Unlike Admin/Index (Admin-only at the page level, covered
        // by UnauthenticatedRequest_ToProtectedPage_RedirectsToLogin above,
        // just for an anonymous visitor instead), this is the one place an
        // authenticated-but-wrong-role POST needs the framework's own
        // Forbid()-handling over the real HTTP pipeline verified, not just a
        // direct PageModel method call (see IndexModelTests in
        // Admin/Schedule, which cover the same rule at that lower level).
        var client = MakeClient();
        var jar = new CookieJar();

        var (_, _, loginToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Login");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Login", new()
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Input.Email"] = "employee@marsvin.dk",
            ["Input.Password"] = "Employee123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, "employee@marsvin.dk");

        var (_, _, scheduleToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Admin/Schedule/Index");
        var response = await HttpTestHelpers.PostForm(client, jar, "/Admin/Schedule/Index?handler=DecideRequest", new()
        {
            ["__RequestVerificationToken"] = scheduleToken,
            ["requestId"] = "1",
            ["approve"] = "true"
        });

        // Cookie authentication's AccessDeniedPath turns a Forbid() result
        // into a redirect, the same as LoginPath does for an unauthenticated
        // request - a raw 403 is never what the browser actually sees here.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task DeactivatedAccount_LosesItsAlreadySignedInSessionOnTheNextRequest()
    {
        // The auth cookie stays valid for as long as it is used - without
        // AuthCookiePrincipal.RevalidateAsync re-checking dbo.Users on every
        // request, deactivating an account would only stop *new* logins.
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar, "deactivated");
        Assert.Equal(HttpStatusCode.OK, (await HttpTestHelpers.Get(client, jar, "/Account/Profile")).StatusCode);

        await ExecuteSql("UPDATE dbo.Users SET IsActive = 0 WHERE Email = @Email;", email);

        var response = await HttpTestHelpers.Get(client, jar, "/Account/Profile");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        // And the cookie itself is cleared, not just ignored for this one request.
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var clearedAuthCookie = Assert.Single(cookies!, c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.Contains("1970", clearedAuthCookie);
    }

    [Fact]
    public async Task DeletedAccount_LosesItsAlreadySignedInSessionOnTheNextRequest()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar, "deleted");

        await ExecuteSql("DELETE FROM dbo.Users WHERE Email = @Email;", email);

        var response = await HttpTestHelpers.Get(client, jar, "/Account/Profile");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task RoleChange_TakesEffectOnTheNextRequest_WithoutSigningInAgain()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar, "rolechange");

        // Signed in as a Customer - the staff dashboard is off limits.
        var asCustomer = await HttpTestHelpers.Get(client, jar, "/Admin/Index");
        Assert.Equal(HttpStatusCode.Found, asCustomer.StatusCode);
        Assert.Contains("/Account/AccessDenied", asCustomer.Headers.Location!.ToString());

        await ExecuteSql($"UPDATE dbo.Users SET Role = {(int)UserRole.Employee} WHERE Email = @Email;", email);
        Assert.Equal(HttpStatusCode.OK, (await HttpTestHelpers.Get(client, jar, "/Admin/Index")).StatusCode);

        // The direction that matters: demoted, same cookie, access gone.
        await ExecuteSql($"UPDATE dbo.Users SET Role = {(int)UserRole.Customer} WHERE Email = @Email;", email);
        var afterDemotion = await HttpTestHelpers.Get(client, jar, "/Admin/Index");
        Assert.Equal(HttpStatusCode.Found, afterDemotion.StatusCode);
        Assert.Contains("/Account/AccessDenied", afterDemotion.Headers.Location!.ToString());
    }

    [Fact]
    public void EveryPage_RequiresLoginUnlessExplicitlyListedAsPublic()
    {
        // Deny by default (see AddRazorPages in Program.cs): a page with no
        // authorization metadata at all would mean the convention is gone and
        // a forgotten [Authorize] is public again.
        var pages = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(e => e.Metadata.GetMetadata<PageActionDescriptor>() is not null)
            .ToList();
        Assert.NotEmpty(pages);

        Assert.All(pages, e => Assert.NotNull(e.Metadata.GetMetadata<IAuthorizeData>()));

        var mustNotBePublic = pages.Where(e =>
        {
            var viewPath = e.Metadata.GetMetadata<PageActionDescriptor>()!.ViewEnginePath;
            return viewPath.StartsWith("/Admin/", StringComparison.Ordinal) || viewPath == "/Account/Profile";
        }).ToList();
        Assert.NotEmpty(mustNotBePublic);
        Assert.All(mustNotBePublic, e => Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>()));
    }

    /// <summary>Registers a fresh Customer and completes the email confirmation, leaving <paramref name="jar"/> signed in. Returns the account's email.</summary>
    [Fact]
    public async Task LogOutEverywhere_EndsTheSessionInAnotherBrowserToo()
    {
        var client = MakeClient();
        var thisBrowser = new CookieJar();
        var email = await RegisterAndSignIn(client, thisBrowser, "everywhere");

        // The same account, logged in a second time somewhere else.
        var otherBrowser = new CookieJar();
        var (_, _, loginToken) = await HttpTestHelpers.GetWithToken(client, otherBrowser, "/Account/Login");
        await HttpTestHelpers.PostForm(client, otherBrowser, "/Account/Login", new()
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Input.Email"] = email,
            ["Input.Password"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, otherBrowser, email);
        Assert.Equal(HttpStatusCode.OK, (await HttpTestHelpers.Get(client, otherBrowser, "/Account/Profile")).StatusCode);

        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, thisBrowser, "/Account/Profile");
        var response = await HttpTestHelpers.PostForm(client, thisBrowser, "/Account/Profile?handler=SignOutEverywhere", new()
        {
            ["__RequestVerificationToken"] = token
        });
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());

        // Neither browser is signed in any more.
        foreach (var jar in new[] { thisBrowser, otherBrowser })
        {
            var profile = await HttpTestHelpers.Get(client, jar, "/Account/Profile");
            Assert.Equal(HttpStatusCode.Found, profile.StatusCode);
            Assert.Contains("/Account/Login", profile.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task ChangingThePassword_EndsASessionThatWasAlreadySignedIn()
    {
        // What a stolen cookie comes down to: someone else holding a session
        // that was signed in before the owner changed the password.
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar, "stolen");

        var users = new MarsvinWebExample.Data.SqlUserAccountStore(MarsvinWebAppFactory.ConnectionString);
        users.UpdatePassword(users.FindByEmail(email)!.UserId, "a-new-password-hash");

        var response = await HttpTestHelpers.Get(client, jar, "/Account/Profile");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PagesWithSomeonesOwnData_AreNotKeptByTheBrowser()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        // A guest's cart (and receipt) may not be kept, signed in or not...
        var cart = await HttpTestHelpers.Get(client, jar, "/Cart");
        Assert.True(cart.Headers.CacheControl!.NoStore);

        // ...and nothing shown to a signed-in user may, the front page included.
        await RegisterAndSignIn(client, jar, "nostore");
        Assert.True((await HttpTestHelpers.Get(client, jar, "/Account/Profile")).Headers.CacheControl!.NoStore);
        Assert.True((await HttpTestHelpers.Get(client, jar, "/")).Headers.CacheControl!.NoStore);
    }

    private static async Task<string> RegisterAndSignIn(HttpClient client, CookieJar jar, string label)
    {
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Revalidated",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
        return email;
    }

    // Straight at MarsvinDb_WebTest, standing in for what an admin does
    // through /Admin/Users in a different browser session.
    private static async Task ExecuteSql(string sql, string email)
    {
        using var connection = new SqlConnection(MarsvinWebAppFactory.ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", email);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}
