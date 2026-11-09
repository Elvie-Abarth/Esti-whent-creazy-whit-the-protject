namespace MarsvinWebExample.Models;

/// <summary>
/// The large common-password list for the "not an obvious password" rule
/// (ASVS 6.2.4 asks that at least the 3000 most common passwords be rejected).
/// Loaded once, lazily, from a plain text file - <c>Data/CommonPasswords.txt</c>,
/// one password per line, blank lines and lines starting with '#' ignored -
/// copied next to the app at build time (see the .csproj). Held in a
/// case-insensitive <see cref="HashSet{T}"/>, so a lookup is O(1).
///
/// Matched EXACTLY: a password that *is* "letmein" is rejected, but a long
/// passphrase that merely contains "letmein" is not. Short, shop-specific
/// words (marsvin, guineapig, butik, ...) are a separate substring list in
/// <see cref="PasswordPolicy"/>; this file is only for whole common passwords.
///
/// No database and no ORM: it is a flat file read straight off disk. If the
/// file is missing or unreadable the set is simply empty - the shop-specific
/// substring list and the structural rules still run, and the app never fails
/// to start over it. The server is always the source of truth; the strength
/// meter (wwwroot/js/password-meter.js) mirrors the same idea client-side.
/// </summary>
public static class CommonPasswordList
{
    private static readonly Lazy<HashSet<string>> Entries = new(Load);

    /// <summary>How many common passwords were loaded (0 when the file is absent).</summary>
    public static int Count => Entries.Value.Count;

    /// <summary>True when the whole password is on the common-password list.</summary>
    public static bool IsCommon(string? password) =>
        !string.IsNullOrEmpty(password) && Entries.Value.Contains(password.Trim());

    private static HashSet<string> Load()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "CommonPasswords.txt");
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    set.Add(line);
                }
            }
        }
        catch
        {
            // A missing or unreadable list must never stop the app - the
            // shop-specific substring list and the structural rules still run.
        }
        return set;
    }
}
