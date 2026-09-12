namespace MarsvinWebExample.Tests;

/// <summary>Shared plumbing for tests that drive the app over real HTTP via MarsvinWebAppFactory.</summary>
internal static class HttpTestHelpers
{
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
