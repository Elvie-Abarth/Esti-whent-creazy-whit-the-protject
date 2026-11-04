namespace MarsvinWebExample.Models;

/// <summary>
/// The one place the password rules live, so the server actually enforces
/// what the strength meter (wwwroot/js/password-meter.js) shows - the meter
/// mirrors these exact five requirements. A password is accepted only when
/// all of them hold:
///
///   1. at least <see cref="MinLength"/> characters
///   2. both upper- and lower-case letters
///   3. a digit
///   4. a symbol (anything that isn't a letter or a digit)
///   5. not an obvious password - not a common one, not built from the
///      shop's own name, and not built from the person's own email or name
///      (a strength meter counting only structure would happily call
///      "Marsvin-Shop-99!" strong; to a guesser it is not)
///
/// This is deliberately stricter than NIST 800-63B, which advises against
/// mandatory composition rules in favour of length plus a breach-list check.
/// The project chooses the stricter, more visible policy on purpose; the
/// blocklist here is a short high-signal list, not a full breach corpus.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    // The passwords people actually reach for first, plus this shop's own
    // words. Matched as substrings and case-insensitively, so "Marsvin2026!"
    // and "xPassword1!" are both caught.
    private static readonly string[] Blocklist =
    [
        "password", "passord", "adgangskode", "123456", "1234567", "12345678",
        "123456789", "1234567890", "qwerty", "qwertyui", "asdfgh", "abc123",
        "111111", "000000", "iloveyou", "letmein", "welcome", "admin", "login",
        "dragon", "monkey", "football", "princess", "sunshine", "superman",
        "marsvin", "guineapig", "guinea", "cavy", "gnavling", "butik"
    ];

    /// <summary>
    /// Null when <paramref name="password"/> is acceptable, otherwise a short
    /// reason in the given language, ready to show as a field error. The
    /// email and display name are passed in so a password built out of them -
    /// which the structural checks alone would pass - is still rejected; pass
    /// null for either when there is nothing to compare against.
    /// </summary>
    public static string? Validate(string? password, string? email, string? displayName, bool english)
    {
        password ??= "";

        if (password.Length < MinLength)
            return english
                ? $"The password must be at least {MinLength} characters."
                : $"Adgangskoden skal være mindst {MinLength} tegn.";

        if (!password.Any(char.IsLower) || !password.Any(char.IsUpper))
            return english
                ? "The password must have both upper and lower case letters."
                : "Adgangskoden skal have både store og små bogstaver.";

        if (!password.Any(char.IsDigit))
            return english
                ? "The password must contain a number."
                : "Adgangskoden skal indeholde et tal.";

        if (password.All(char.IsLetterOrDigit))
            return english
                ? "The password must contain a symbol, for example ! or ?."
                : "Adgangskoden skal indeholde et tegn, for eksempel ! eller ?.";

        if (IsObvious(password, email, displayName))
            return english
                ? "That password is too easy to guess - avoid common words and your own name or email."
                : "Den adgangskode er for nem at gætte - undgå almindelige ord og dit eget navn eller din e-mail.";

        return null;
    }

    /// <summary>
    /// Whether the password is one a guesser would try early: on the
    /// blocklist, or containing the shop's own name, or built from this
    /// person's own email local part or display name. Shared so the meter and
    /// the server agree on what "obvious" means.
    /// </summary>
    public static bool IsObvious(string password, string? email, string? displayName)
    {
        var lower = (password ?? "").ToLowerInvariant();
        if (lower.Length == 0) return false;

        foreach (var word in Blocklist)
            if (lower.Contains(word)) return true;

        // A fragment of three or more characters is enough: "anna" in
        // "Anna-is-the-best-1!" is still the account's own name.
        var localPart = (email ?? "").Split('@')[0].ToLowerInvariant();
        if (localPart.Length >= 3 && lower.Contains(localPart)) return true;

        var name = (displayName ?? "").Trim().ToLowerInvariant();
        if (name.Length >= 3 && lower.Contains(name)) return true;

        return false;
    }
}
