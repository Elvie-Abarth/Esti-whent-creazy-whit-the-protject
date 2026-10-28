using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MarsvinWebExample.Tests.Pages.Account;

/// <summary>
/// The JSON API under /api and its JWT bearer authentication, over real HTTP:
/// who can get a token, what a token opens, and everything that must make a
/// token useless (tampering, expiry, a changed account, the wrong audience).
/// </summary>
[Collection("WebApp collection")]
public class EndToEndJwtApiTests(MarsvinWebAppFactory factory)
{
    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    private static async Task SignInAs(HttpClient client, CookieJar jar, string email, string password)
    {
        var (_, _, loginToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Login");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Login", new()
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
    }

    private static async Task<string> RegisterAndSignIn(HttpClient client, CookieJar jar)
    {
        var email = $"jwt-{Guid.NewGuid():N}@example.com";
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Token Kunde",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
        return email;
    }

    // Asks the token endpoint for a JWT using the signed-in cookie.
    private static async Task<string> MintToken(HttpClient client, CookieJar jar)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
        jar.Apply(request);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetWithBearer(HttpClient client, string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static async Task<string> TokenFor(HttpClient client, string email, string password)
    {
        var jar = new CookieJar();
        await SignInAs(client, jar, email, password);
        return await MintToken(client, jar);
    }

    private string ForgeToken(Action<SecurityTokenDescriptor> change)
    {
        var settings = factory.Services.GetRequiredService<JwtSettings>();
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtTokens.SubjectClaim, "1"),
                new Claim(JwtTokens.RoleClaim, "Admin"),
                new Claim(JwtTokens.StampClaim, "x")
            ]),
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };
        change(descriptor);
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static async Task ExecuteSql(string sql, string email)
    {
        using var connection = new SqlConnection(MarsvinWebAppFactory.ConnectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Email", email);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---- getting a token ----

    [Fact]
    public async Task TheTokenEndpoint_HandsNothingToSomeoneWhoIsNotSignedIn()
    {
        var response = await MakeClient().PostAsync("/api/auth/token", content: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ASignedInVisitor_GetsAShortLivedThreePartToken_ThatIsNeverCached()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
        jar.Apply(request);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("token").GetString()!.Split('.').Length);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        var lifetime = body.GetProperty("expiresAt").GetDateTimeOffset() - DateTimeOffset.UtcNow;
        Assert.InRange(lifetime, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));
    }

    [Fact]
    public async Task TheToken_CarriesTheClaims_ButNeverThePasswordHash()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(await MintToken(client, jar));

        Assert.Equal("HS256", token.Alg);
        Assert.Equal(email, token.GetClaim(JwtTokens.EmailClaim).Value);
        Assert.Equal("Customer", token.GetClaim(JwtTokens.RoleClaim).Value);
        Assert.DoesNotContain("hash", token.EncodedPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(token.EncodedPayload)),
            "SomePass123!");
    }

    // ---- using a token ----

    [Fact]
    public async Task WithAValidToken_TheApiKnowsWhoYouAre()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);

        var response = await GetWithBearer(client, "/api/me", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Customer", body.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    public async Task WithoutAUsableToken_TheApiAnswers401_NotARedirectToTheLoginPage(string? token)
    {
        var response = await GetWithBearer(MakeClient(), "/api/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task TheLoginCookieAlone_DoesNotOpenTheApi()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        jar.Apply(request);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ABearerToken_DoesNotOpenTheWebsitePages()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);

        var response = await GetWithBearer(client, "/Account/Profile", token);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    // ---- roles (RBAC) ----

    [Fact]
    public async Task ACustomerToken_CannotReachTheStaffOrAdminEndpoints()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetWithBearer(client, "/api/admin/orders", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWithBearer(client, "/api/admin/users", token)).StatusCode);
    }

    [Fact]
    public async Task AnEmployeeToken_ReachesTheOrders_ButNotTheAccounts()
    {
        var client = MakeClient();
        var token = await TokenFor(client, "employee@marsvin.dk", "Employee123!");

        Assert.Equal(HttpStatusCode.OK, (await GetWithBearer(client, "/api/admin/orders", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetWithBearer(client, "/api/admin/users", token)).StatusCode);
    }

    [Fact]
    public async Task AnAdminToken_ReachesEverything_AndTheAccountListNeverShowsPasswords()
    {
        var client = MakeClient();
        var token = await TokenFor(client, "admin@marsvin.dk", "Admin123!");

        Assert.Equal(HttpStatusCode.OK, (await GetWithBearer(client, "/api/admin/orders", token)).StatusCode);
        var users = await GetWithBearer(client, "/api/admin/users", token);
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        var text = await users.Content.ReadAsStringAsync();
        Assert.Contains("admin@marsvin.dk", text);
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheOrderList_IsTheCallersOwn_AndSomeoneElsesOrderIsNotFound()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);

        var own = await GetWithBearer(client, "/api/orders", token);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(0, (await own.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        // Whatever number is tried, it is looked up for the caller only.
        Assert.Equal(HttpStatusCode.NotFound, (await GetWithBearer(client, "/api/orders/1", token)).StatusCode);
    }

    // ---- tokens that must not work ----

    [Fact]
    public async Task ATokenWhosePayloadWasEdited_IsRefused()
    {
        // The classic attack: take a customer's token and rewrite the role.
        var client = MakeClient();
        var jar = new CookieJar();
        await RegisterAndSignIn(client, jar);
        var parts = (await MintToken(client, jar)).Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("Customer", "Admin");
        var tampered = $"{parts[0]}.{Base64Url(payload)}.{parts[2]}";

        var response = await GetWithBearer(client, "/api/admin/users", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenThatSaysItNeedsNoSignature_IsRefused()
    {
        var unsigned = $"{Base64Url("""{"alg":"none","typ":"JWT"}""")}.{Base64Url("""{"sub":"1","role":"Admin"}""")}.";

        var response = await GetWithBearer(MakeClient(), "/api/admin/users", unsigned);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsRefused()
    {
        var foreign = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity([new Claim(JwtTokens.SubjectClaim, "1"), new Claim(JwtTokens.RoleClaim, "Admin")]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(MakeClient(), "/api/me", foreign)).StatusCode);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefused()
    {
        var expired = ForgeToken(d =>
        {
            d.NotBefore = DateTime.UtcNow.AddHours(-2);
            d.Expires = DateTime.UtcNow.AddHours(-1);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(MakeClient(), "/api/me", expired)).StatusCode);
    }

    [Theory]
    [InlineData("other-issuer", JwtSettings.Audience)]
    [InlineData(JwtSettings.Issuer, "other-api")]
    public async Task ATokenMeantForSomeoneElse_IsRefused(string issuer, string audience)
    {
        var token = ForgeToken(d =>
        {
            d.Issuer = issuer;
            d.Audience = audience;
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(MakeClient(), "/api/me", token)).StatusCode);
    }

    // ---- an account that changes after the token was issued ----

    [Fact]
    public async Task DeactivatingTheAccount_KillsItsTokenAtOnce()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);
        Assert.Equal(HttpStatusCode.OK, (await GetWithBearer(client, "/api/me", token)).StatusCode);

        await ExecuteSql("UPDATE dbo.Users SET IsActive = 0 WHERE Email = @Email;", email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(client, "/api/me", token)).StatusCode);
    }

    [Fact]
    public async Task ChangingThePassword_KillsTokensIssuedBefore()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar);
        var token = await MintToken(client, jar);

        var users = new SqlUserAccountStore(MarsvinWebAppFactory.ConnectionString);
        users.UpdatePassword(users.FindByEmail(email)!.UserId, "a-new-password-hash");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(client, "/api/me", token)).StatusCode);
    }

    [Fact]
    public async Task ADemotedAdmin_LosesTheAdminApiWithTheTokenTheyAlreadyHold()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        var email = await RegisterAndSignIn(client, jar);
        await ExecuteSql($"UPDATE dbo.Users SET Role = {(int)UserRole.Admin} WHERE Email = @Email;", email);
        var adminJar = new CookieJar();
        await SignInAs(client, adminJar, email, "SomePass123!");
        var token = await MintToken(client, adminJar);
        Assert.Equal(HttpStatusCode.OK, (await GetWithBearer(client, "/api/admin/users", token)).StatusCode);

        await ExecuteSql($"UPDATE dbo.Users SET Role = {(int)UserRole.Customer} WHERE Email = @Email;", email);

        // The token still says Admin, and is still signed and unexpired - the database wins.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetWithBearer(client, "/api/admin/users", token)).StatusCode);
    }
}
