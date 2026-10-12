using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests.Pages;

/// <summary>
/// The error pages, the privacy page and the rate limits, over real HTTP:
/// each error status gets its own explanation and never shows anything from
/// the failed request, and too many attempts from one client are actually
/// turned away.
/// </summary>
[Collection("WebApp collection")]
public class EndToEndErrorPagesAndRateLimitTests(MarsvinWebAppFactory factory)
{
    private static readonly WebApplicationFactoryClientOptions NoRedirects = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    };

    private HttpClient MakeClient() => factory.CreateClient(NoRedirects);

    private static async Task<string> Html(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task APageThatDoesNotExist_Is404_WithTheShopsOwnExplanation()
    {
        var response = await MakeClient().GetAsync("/denne-side-findes-ikke");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await Html(response);
        Assert.Contains("Den side findes ikke", html);
        Assert.Contains("Se marsvinene", html);
    }

    [Fact]
    public async Task TheErrorPage_NeverShowsTheAddressThatWasAskedFor()
    {
        // The 404 page is a fixed text - a script tag in the URL has nowhere to be echoed.
        var response = await MakeClient().GetAsync("/findes-ikke-<script>alert(1)</script>");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("alert(1)", raw);
        Assert.DoesNotContain("findes-ikke-", raw);
    }

    [Theory]
    [InlineData(400, "Det gik ikke igennem")]
    [InlineData(403, "Du har ikke adgang til den side")]
    [InlineData(429, "For mange forsøg")]
    [InlineData(404, "Den side findes ikke")]
    [InlineData(500, "Den side findes ikke")]      // anything unexpected is treated as "not found"
    [InlineData(999, "Den side findes ikke")]
    public async Task TheErrorPage_ExplainsEachStatusItKnows_AndTreatsTheRestAsNotFound(int code, string expectedHeading)
    {
        var response = await MakeClient().GetAsync($"/Error?code={code}");

        Assert.Contains(expectedHeading, await Html(response));
    }

    [Fact]
    public async Task TheErrorPage_IsNotCached()
    {
        var response = await MakeClient().GetAsync("/Error?code=403");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task TheServerErrorPage_SaysSomethingWentWrong_AndNothingAboutWhat()
    {
        var response = await MakeClient().GetAsync("/ServerError");

        var html = await Html(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Der gik noget galt hos os", html);
        Assert.DoesNotContain("Exception", html);
        Assert.DoesNotContain("   at ", html);            // no stack trace lines
    }

    [Fact]
    public async Task ThePrivacyPage_IsPublic_AndNamesTheLanguageCookie()
    {
        var response = await MakeClient().GetAsync("/Privatliv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await Html(response);
        Assert.Contains("Vi bruger ikke tracking-cookies", html);
        Assert.Contains("dansk eller", html);
    }

    // ---- Rate limiting ----
    //
    // The shared factory raises the "auth" limit to 5000 so the rest of the
    // suite isn't throttled. These tests need the limit to bite, so they
    // start their own copy of the app with it turned down - a separate host
    // has its own limiter, and nothing here spends anyone else's permits.

    private WebApplicationFactory<Program> AppWithAuthLimit(int permits) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthPermitLimit", permits.ToString()));

    [Fact]
    public async Task TooManyRequestsToTheLoginPage_AreTurnedAwayWith429_AndTheFriendlyPage()
    {
        using var app = AppWithAuthLimit(3);
        var client = app.CreateClient(NoRedirects);

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await client.GetAsync("/Account/Login");
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, statuses[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Contains("For mange forsøg", await Html(last!));
    }

    [Fact]
    public async Task TheLimit_IsSharedAcrossLoginRegisterAndForgotPassword()
    {
        // One budget for all three: spreading attempts over the pages doesn't buy more of them.
        using var app = AppWithAuthLimit(3);
        var client = app.CreateClient(NoRedirects);
        string[] pages = ["/Account/Login", "/Account/Register", "/Account/ForgotPassword"];

        foreach (var page in pages)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);

        foreach (var page in pages)
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync(page)).StatusCode);
    }

    [Fact]
    public async Task ARateLimitedClient_CanStillUseTheRestOfTheSite()
    {
        using var app = AppWithAuthLimit(1);
        var client = app.CreateClient(NoRedirects);
        await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/Account/Login")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Tilbehor")).StatusCode);
    }

    [Fact]
    public async Task TheChat_StopsAnsweringAfterTwentyMessagesInAMinute()
    {
        // The chat's limit is fixed at 20 a minute. Its own copy of the app,
        // so the chat tests elsewhere keep their full allowance.
        using var app = AppWithAuthLimit(5000);
        var client = app.CreateClient(NoRedirects);
        var jar = new CookieJar();
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Faq");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 22; i++)
        {
            var response = await HttpTestHelpers.PostForm(client, jar, "/Chat", new()
            {
                ["__RequestVerificationToken"] = token,
                ["message"] = "Hvornår har I åbent?",
                ["lang"] = "da"
            });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(20), s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}
