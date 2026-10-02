using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Confirms the defence-in-depth headers Program.cs adds on every response
/// actually reach the wire, and that HSTS is configured with the real
/// recommended values (2 years, includeSubDomains, preload) rather than
/// ASP.NET Core's much weaker framework defaults (30 days, neither of the
/// other two) - a config mistake here is invisible in normal manual testing
/// since the dev profile never runs outside Development, where HSTS never
/// activates at all.
/// </summary>
[Collection("WebApp collection")]
public class SecurityHeadersTests(MarsvinWebAppFactory factory)
{
    [Fact]
    public async Task AnyResponse_CarriesTheDefenceInDepthHeaders()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
    }

    [Fact]
    public async Task OutsideDevelopment_HstsHeaderUsesTheRecommendedValuesNotTheFrameworkDefaults()
    {
        // HSTS is only wired up for a non-Development environment (see
        // Program.cs - it's meaningless against http://localhost), so the
        // shared Development-mode factory from every other test never sends
        // it; this spins up its own instance with the environment overridden
        // to prove the header itself, once it does apply, isn't just
        // UseHsts()'s bare 30-day/no-subdomains/no-preload default.
        using var prodFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        // UseHsts()'s own framework default excludes the "localhost" host
        // specifically (so the header never fires against a plain local
        // dev box) - WebApplicationFactory's client defaults its base
        // address to exactly that host, which would silently skip the
        // header entirely and defeat this test. A non-excluded host name
        // is enough; TestServer never does real DNS resolution.
        var client = prodFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://marsvin.example/")
        });

        var response = await client.GetAsync("/");

        var hsts = response.Headers.GetValues("Strict-Transport-Security").Single();
        Assert.Contains("max-age=63072000", hsts); // 730 days
        Assert.Contains("includeSubDomains", hsts);
        Assert.Contains("preload", hsts);
    }
}
