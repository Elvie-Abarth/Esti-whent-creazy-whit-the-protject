using System.Text.RegularExpressions;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Minimal manual cookie jar for integration tests. Deliberately not using
/// HttpClientHandler's automatic cookie container: tests here need to
/// inspect raw Set-Cookie headers (HttpOnly flag, expiry on logout) and
/// sometimes deliberately send a mismatched or missing cookie/token pair to
/// prove CSRF protection rejects it - both need full manual control.
/// </summary>
public sealed class CookieJar
{
    private readonly Dictionary<string, string> _cookies = new();

    public void Capture(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies)) return;

        foreach (var setCookie in setCookies)
        {
            var nameValue = setCookie.Split(';', 2)[0];
            var parts = nameValue.Split('=', 2);
            if (parts.Length != 2) continue;
            _cookies[parts[0].Trim()] = parts[1].Trim();
        }
    }

    public void Apply(HttpRequestMessage request)
    {
        if (_cookies.Count == 0) return;
        request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, """__RequestVerificationToken"[^>]*value="([^"]*)""");
        if (!match.Success)
            throw new InvalidOperationException("No antiforgery token found in the response HTML.");
        return match.Groups[1].Value;
    }
}
