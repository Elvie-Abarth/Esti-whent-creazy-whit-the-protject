namespace MarsvinWebExample.Models;

/// <summary>
/// The one place the password rules live, so the server actually enforces
/// what the strength meter (wwwroot/js/password-meter.js) shows - the meter
/// mirrors these exact five requirements. A password is accepted only when
/// all of them hold:
///
///   1. at least <see cref="MinLength"/> characters, or <see cref="AdminMinLength"/>
///      for an Admin account - an admin can change prices, roles and other
///      accounts, so its password is held to a longer minimum (that account
///      also carries 2FA where enabled)
///   2. both upper- and lower-case letters
///   3. a digit
///   4. a symbol (anything that isn't a letter or a digit)
///   5. not an obvious password - not a common one, not built from the
///      shop's own name, and not built from the person's own email or name
///      (a strength meter counting only structure would happily call
///      "Marsvin-Shop-99!" strong; to a guesser it is not)
///
/// "Not a common one" is two different checks (see <see cref="IsObvious"/>):
///   - a short, shop-specific <see cref="ShopBlocklist"/>, matched as a
///     *substring* - "marsvin", "guineapig", "butik" and the literal word
///     "password" are never wanted anywhere inside a password;
///   - the large common-password list (<see cref="CommonPasswordList"/>, loaded
///     from Data/CommonPasswords.txt), matched *exactly* - so "letmein" is
///     rejected but a long passphrase that merely contains "letmein" is not.
///
/// This is deliberately stricter than NIST 800-63B on composition, while the
/// exact-match common-password list is the breach-list check both NIST and
/// ASVS 6.2.4 ask for.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>The minimum length for customers and employees.</summary>
    public const int MinLength = 12;

    /// <summary>The minimum length for an Admin account - stricter, because it
    /// can change prices, roles and other accounts.</summary>
    public const int AdminMinLength = 15;

    /// <summary>The minimum length this role's password must reach.</summary>
    public static int MinLengthFor(UserRole role) =>
        role == UserRole.Admin ? AdminMinLength : MinLength;

    // Shop-specific and "password"-literal words we never want ANYWHERE inside
    // a password, matched as substrings and case-insensitively, so "Marsvin2026!"
    // and "xPassword1!" are both caught. Generic common passwords (123456,
    // qwerty, dragon, letmein, ...) are NOT here - they live in the large
    // exact-match list (CommonPasswordList), so a long passphrase that merely
    // contains "dragon" is still allowed; only a password that *is* a common
    // one is rejected.
    private static readonly string[] ShopBlocklist =
    [
        "password", "passord", "adgangskode",
        "marsvin", "guineapig", "guinea", "cavy", "gnavling", "butik"
    ];

    /// <summary>
    /// Null when <paramref name="password"/> is acceptable, otherwise a short
    /// reason in the given language, ready to show as a field error. The
    /// email and display name are passed in so a password built out of them -
    /// which the structural checks alone would pass - is still rejected; pass
    /// null for either when there is nothing to compare against.
    /// </summary>
    public static string? Validate(string? password, string? email, string? displayName, bool english, UserRole role = UserRole.Customer)
    {
        password ??= "";

        var minLength = MinLengthFor(role);
        if (password.Length < minLength)
            return english
                ? $"The password must be at least {minLength} characters."
                : $"Adgangskoden skal være mindst {minLength} tegn.";

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
    /// Whether the password is one a guesser would try early: exactly equal to
    /// a common password, or containing a shop-specific word, or built from this
    /// person's own email local part or display name. Shared so the meter and
    /// the server agree on what "obvious" means.
    /// </summary>
    public static bool IsObvious(string password, string? email, string? displayName)
    {
        var lower = (password ?? "").ToLowerInvariant();
        if (lower.Length == 0) return false;

        // A whole password that IS a common one (exact match, case-insensitive)
        // - but not a long passphrase that merely contains a common word.
        if (CommonPasswordList.IsCommon(lower)) return true;

        foreach (var word in ShopBlocklist)
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
