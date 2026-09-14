using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests;

/// <summary>Shared plumbing for tests that drive the app over real HTTP via MarsvinWebAppFactory.</summary>
internal static class HttpTestHelpers
{
    /// <summary>
    /// Completes the email-confirmation step for Register/Login over real
    /// HTTP without needing to intercept the actual email: since only the
    /// token's hash is ever stored (see IPendingLoginStore), a raw token
    /// generated here and inserted directly into MarsvinDb_WebTest is
    /// exactly as valid as one the app itself would have generated and
    /// mailed out. GETting ConfirmLogin with it is what actually signs the
    /// session in - the preceding Register/Login POST only gets as far as
    /// "check your email".
    /// </summary>
    public static async Task<HttpResponseMessage> CompleteEmailConfirmation(
        HttpClient client, CookieJar jar, string email)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        using (var connection = new SqlConnection(MarsvinWebAppFactory.ConnectionString))
        {
            await connection.OpenAsync();
            // UPDATEs the row Register/Login's own call to pendingLogins.Create
            // already inserted, rather than inserting a fresh one - swapping in
            // a token this test knows the raw value of while leaving that row's
            // ReturnUrl (set from the actual Register/Login request) untouched.
            // A fresh INSERT here would leave ReturnUrl null and silently
            // change where confirming lands.
            using var command = new SqlCommand(
                """
                UPDATE pl SET TokenHash = @TokenHash, ExpiresAt = DATEADD(minute, 15, SYSUTCDATETIME())
                FROM dbo.PendingLogins pl JOIN dbo.Users u ON u.UserId = pl.UserId
                WHERE u.Email = @Email;
                """, connection);
            command.Parameters.AddWithValue("@TokenHash", tokenHash);
            command.Parameters.AddWithValue("@Email", email);
            var rowsUpdated = await command.ExecuteNonQueryAsync();
            if (rowsUpdated == 0)
            {
                throw new InvalidOperationException(
                    $"No pending login found for {email} - call this right after a Register/Login POST for that address.");
            }
        }

        return await Get(client, jar, "/Account/ConfirmLogin?token=" + Uri.EscapeDataString(rawToken));
    }

    public static async Task<(HttpResponseMessage Response, string Html, string Token)> GetWithToken(
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

    public static async Task<HttpResponseMessage> PostForm(
        HttpClient client, CookieJar jar, string path, Dictionary<string, string> fields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        jar.Apply(request);
        var response = await client.SendAsync(request);
        jar.Capture(response);
        return response;
    }

    public static async Task<HttpResponseMessage> Get(HttpClient client, CookieJar jar, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        jar.Apply(request);
        var response = await client.SendAsync(request);
        jar.Capture(response);
        return response;
    }
}
