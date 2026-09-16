namespace MarsvinWebExample.Data;

/// <summary>
/// Checks a submitted "g-recaptcha-response" token with Google. See
/// GoogleRecaptchaVerifier for what happens when no keys are configured.
/// </summary>
public interface IRecaptchaVerifier
{
    Task<bool> VerifyAsync(string? token);
}
