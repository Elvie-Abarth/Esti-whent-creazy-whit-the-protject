namespace MarsvinWebExample.Data;

/// <summary>
/// Bound from the "Recaptcha" config section. SiteKey is public - it's
/// embedded straight into the page HTML, so appsettings.json is fine for it.
/// SecretKey is not: set it locally with
/// `dotnet user-secrets set Recaptcha:SecretKey ...`, the same way
/// Email:Password never ends up in a file that gets committed.
/// </summary>
public sealed class RecaptchaOptions
{
    public string SiteKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
