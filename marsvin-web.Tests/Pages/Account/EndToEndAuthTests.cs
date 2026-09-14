using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

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

        // 2. Now authenticated - a Customer-only page should be reachable.
        var cartRequest = new HttpRequestMessage(HttpMethod.Get, "/Cart/Index");
        jar.Apply(cartRequest);
        var cartResponse = await client.SendAsync(cartRequest);
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);

        // 3. Log out (with a valid antiforgery token from that same cart page).
        var cartHtml = await cartResponse.Content.ReadAsStringAsync();
        var logoutToken = CookieJar.ExtractAntiforgeryToken(cartHtml);
        var logoutResponse = await HttpTestHelpers.PostForm(client, jar, "/Account/Logout", new()
        {
            ["__RequestVerificationToken"] = logoutToken
        });
        Assert.Equal(HttpStatusCode.Found, logoutResponse.StatusCode);
        Assert.True(logoutResponse.Headers.TryGetValues("Set-Cookie", out var clearingCookies));
        var clearedAuthCookie = Assert.Single(clearingCookies!, c => c.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.Contains("1970", clearedAuthCookie); // expired in the past - browsers delete it

        // 4. The auth cookie value is now cleared - the same protected page must redirect to login again.
        var afterLogoutRequest = new HttpRequestMessage(HttpMethod.Get, "/Cart/Index");
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
}
