using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests.Pages.Account;

/// <summary>
/// Drives the real app over HTTP (routing, authentication, authorization,
/// and - the part unit tests calling PageModel methods directly can't see -
/// antiforgery/CSRF validation and actual cookie behavior).
/// </summary>
public class EndToEndAuthTests(MarsvinWebAppFactory factory) : IClassFixture<MarsvinWebAppFactory>
{
    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    private static async Task<(HttpResponseMessage Response, string Html, string Token)> GetWithToken(
        HttpClient client, CookieJar jar, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        jar.Apply(request);
        var response = await client.SendAsync(request);
        jar.Capture(response);
        var html = await response.Content.ReadAsStringAsync();
        var token = CookieJar.ExtractAntiforgeryToken(html);
        return (response, html, token);
    }

    private static async Task<HttpResponseMessage> PostForm(
        HttpClient client, CookieJar jar, string path, Dictionary<string, string> fields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        jar.Apply(request);
        var response = await client.SendAsync(request);
        jar.Capture(response);
        return response;
    }

    [Fact]
    public async Task Get_RegisterPage_ReturnsAntiforgeryTokenAndCookie()
    {
        var client = MakeClient();
        var jar = new CookieJar();

        var (response, _, token) = await GetWithToken(client, jar, "/Account/Register");

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
        await GetWithToken(client, jar, "/Account/Register"); // establishes the antiforgery cookie

        var email = $"no-token-{Guid.NewGuid():N}@example.com";
        var response = await PostForm(client, jar, "/Account/Register", new()
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
        await GetWithToken(client, jarA, "/Account/Register");

        var jarB = new CookieJar();
        var (_, _, tokenFromSessionB) = await GetWithToken(client, jarB, "/Account/Register");

        var email = $"mismatched-{Guid.NewGuid():N}@example.com";
        var response = await PostForm(client, jarA, "/Account/Register", new()
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

        // 1. Register - a fresh Customer account, signed in immediately.
        var (_, _, registerToken) = await GetWithToken(client, jar, "/Account/Register");
        var registerResponse = await PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Full Flow",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        Assert.Equal(HttpStatusCode.Found, registerResponse.StatusCode);
        Assert.True(registerResponse.Headers.TryGetValues("Set-Cookie", out var authCookies));
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
        var logoutResponse = await PostForm(client, jar, "/Account/Logout", new()
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

        var (_, _, registerToken) = await GetWithToken(client, jar, "/Account/Register");
        await PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Logout Guard",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });

        var logoutResponse = await PostForm(client, jar, "/Account/Logout", new());
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
}
