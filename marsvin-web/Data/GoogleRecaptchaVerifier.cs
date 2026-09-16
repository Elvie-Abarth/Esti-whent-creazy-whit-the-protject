using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MarsvinWebExample.Data;

/// <summary>
/// Verifies a reCAPTCHA v2 token against Google's siteverify endpoint. Skips
/// verification entirely (always returns true) when no Recaptcha:SecretKey
/// is configured - the same fallback LoggingEmailSender uses in place of
/// SmtpEmailSender - so Login/Register/ForgotPassword stay usable out of the
/// box for anyone who clones the repo without setting up their own Google
/// reCAPTCHA keys. _Recaptcha.cshtml applies the matching rule client-side:
/// no SiteKey configured means no widget is rendered at all.
/// </summary>
public sealed class GoogleRecaptchaVerifier(
    HttpClient httpClient, IOptions<RecaptchaOptions> options, ILogger<GoogleRecaptchaVerifier> logger)
    : IRecaptchaVerifier
{
    public async Task<bool> VerifyAsync(string? token)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            logger.LogDebug("No Recaptcha:SecretKey configured - skipping verification.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            using var response = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secretKey,
                    ["response"] = token
                }));

            if (!response.IsSuccessStatusCode) return false;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            return document.RootElement.TryGetProperty("success", out var success) && success.GetBoolean();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Google unreachable or returned something unparseable - fail
            // closed (reject the submission) rather than silently letting an
            // unverified request through.
            logger.LogWarning(ex, "reCAPTCHA verification failed.");
            return false;
        }
    }
}
