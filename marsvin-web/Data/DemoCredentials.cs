using System.Security.Cryptography;
using System.Text;
using MarsvinWebExample.Models;
using Microsoft.Extensions.Configuration;

namespace MarsvinWebExample.Data;

/// <summary>
/// Where the seeded demo accounts' passwords come from - deliberately NOT a
/// literal in source or in any committed document.
///
/// Order of preference:
///   1. configuration - <c>Demo:AdminPassword</c> / <c>Demo:StaffPassword</c>
///      from user-secrets or environment variables (this is also how the test
///      harness injects a fixed, throwaway value);
///   2. otherwise a fresh cryptographically-random password per account,
///      written ONLY to a git-ignored local file (<c>demo-credentials.local.txt</c>
///      in the app folder) so the developer can read it - never logged, never
///      committed.
///
/// The generated passwords are built to satisfy <see cref="PasswordPolicy"/>
/// for their role (Admin needs 15+ characters), so the seed never produces a
/// password the server would itself reject.
/// </summary>
public static class DemoCredentials
{
    public const string AdminKey = "Demo:AdminPassword";
    public const string StaffKey = "Demo:StaffPassword";
    public const string FileName = "demo-credentials.local.txt";

    // Ambiguous characters (0/O, 1/l/I) left out so a password read off the
    // local file and typed by hand doesn't get mistyped.
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "-_.!?*#";

    /// <summary>
    /// The (admin, staff) passwords to seed with. Reads them from configuration;
    /// for any that isn't set, generates a random one and writes the whole demo
    /// credential set to the git-ignored local file (only when a content root is
    /// given and something was actually generated).
    /// </summary>
    public static (string Admin, string Staff) Resolve(IConfiguration? config, string? contentRootPath)
    {
        var configuredAdmin = Configured(config, AdminKey);
        var configuredStaff = Configured(config, StaffKey);

        var admin = configuredAdmin ?? Generate(UserRole.Admin);
        var staff = configuredStaff ?? Generate(UserRole.Employee);

        if ((configuredAdmin is null || configuredStaff is null) && contentRootPath is not null)
            WriteLocalFile(contentRootPath, admin, staff);

        return (admin, staff);
    }

    private static string? Configured(IConfiguration? config, string key)
    {
        var value = config?[key];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>A random password that passes <see cref="PasswordPolicy"/> for the role.</summary>
    public static string Generate(UserRole role)
    {
        var length = Math.Max(PasswordPolicy.MinLengthFor(role) + 3, role == UserRole.Admin ? 18 : 15);
        var all = Lower + Upper + Digits + Symbols;

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var chars = new List<char>(length)
            {
                Pick(Lower), Pick(Upper), Pick(Digits), Pick(Symbols)
            };
            while (chars.Count < length) chars.Add(Pick(all));
            Shuffle(chars);

            var candidate = new string(chars.ToArray());
            // Verified against the real policy - the random draw essentially
            // never hits a common/shop word, but this makes that a guarantee.
            if (PasswordPolicy.Validate(candidate, null, null, english: false, role) is null)
                return candidate;
        }

        throw new InvalidOperationException("Could not generate a policy-passing demo password.");
    }

    private static char Pick(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];

    private static void Shuffle(IList<char> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static void WriteLocalFile(string contentRootPath, string admin, string staff)
    {
        try
        {
            var text = new StringBuilder()
                .AppendLine("# LOCAL ONLY - generated on first run, git-ignored, safe to delete.")
                .AppendLine("# These demo accounts exist only in your local MarsvinDb and grant")
                .AppendLine("# nothing anywhere else. Set your own instead with:")
                .AppendLine("#   dotnet user-secrets set \"Demo:AdminPassword\" <value>")
                .AppendLine("#   dotnet user-secrets set \"Demo:StaffPassword\" <value>")
                .AppendLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                .AppendLine()
                .AppendLine("admin@marsvin.dk      " + admin)
                .AppendLine("employee@marsvin.dk   " + staff)
                .AppendLine("employee2@marsvin.dk  " + staff)
                .AppendLine("employee3@marsvin.dk  " + staff)
                .AppendLine("employee4@marsvin.dk  " + staff)
                .ToString();
            File.WriteAllText(Path.Combine(contentRootPath, FileName), text);
        }
        catch
        {
            // Never fail startup over the convenience file - the accounts are
            // still seeded; the developer can set Demo:* in user-secrets instead.
        }
    }
}
