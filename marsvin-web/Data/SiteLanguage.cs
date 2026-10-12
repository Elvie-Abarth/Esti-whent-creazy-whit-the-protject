namespace MarsvinWebExample.Data;

/// <summary>
/// The DA/EN choice is made and kept in the browser (lang-toggle.js). The
/// server only needs it for emails, and reads it from the small cookie that
/// script sets alongside. The value is never echoed anywhere: it is compared
/// against "en" and "da", and anything else is treated as not set.
/// </summary>
public static class SiteLanguage
{
    public const string CookieName = "marsvin-lang";

    /// <summary>True for English, false for Danish, null when the browser hasn't said (no cookie, or not a value we know).</summary>
    public static bool? Read(HttpContext? httpContext) => httpContext?.Request.Cookies[CookieName] switch
    {
        "en" => true,
        "da" => false,
        _ => null
    };

    public static bool IsEnglish(HttpContext? httpContext) => Read(httpContext) == true;
}
