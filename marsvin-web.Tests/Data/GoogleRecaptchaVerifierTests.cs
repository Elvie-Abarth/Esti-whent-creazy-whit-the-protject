using MarsvinWebExample.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarsvinWebExample.Tests.Data;

public class GoogleRecaptchaVerifierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("some-token")]
    public async Task VerifyAsync_NoSecretKeyConfigured_AlwaysPasses(string? token)
    {
        // No Recaptcha:SecretKey set - the same state as a freshly cloned
        // repo with no Google keys configured. Verification must be a no-op
        // here, the same way GoogleRecaptchaVerifier stands in for a real
        // check, or Login/Register/ForgotPassword would reject every real
        // submission on a machine nobody has set reCAPTCHA up on yet.
        var options = Options.Create(new RecaptchaOptions { SiteKey = "", SecretKey = "" });
        var verifier = new GoogleRecaptchaVerifier(
            new HttpClient(), options, NullLogger<GoogleRecaptchaVerifier>.Instance);

        var result = await verifier.VerifyAsync(token);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_SecretKeyConfigured_NullToken_Fails()
    {
        // With a real secret key configured, a missing token (the widget
        // was never completed, or a bot skipped it entirely) must be
        // rejected without even calling out to Google.
        var options = Options.Create(new RecaptchaOptions { SiteKey = "site-key", SecretKey = "secret-key" });
        var verifier = new GoogleRecaptchaVerifier(
            new HttpClient(), options, NullLogger<GoogleRecaptchaVerifier>.Instance);

        var result = await verifier.VerifyAsync(null);

        Assert.False(result);
    }
}
