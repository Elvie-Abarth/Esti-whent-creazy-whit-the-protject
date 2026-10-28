using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MarsvinWebExample.Data;

/// <summary>
/// The signing key, issuer, audience and lifetime of the API's JWTs.
/// Built once in Program.cs from the Jwt section of the configuration.
/// </summary>
public sealed class JwtSettings
{
    public const string Issuer = "marsvin-web";
    public const string Audience = "marsvin-api";

    /// <summary>Short on purpose: a JWT cannot be called back once issued (see JWT-NOTES.txt), so it simply doesn't live long.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public required SymmetricSecurityKey SigningKey { get; init; }

    /// <summary>
    /// Jwt:Key from configuration (user-secrets or an environment variable,
    /// never appsettings.json) - at least 32 bytes, which is what HS256
    /// needs to be worth its name. Without one the app invents a random key
    /// for this run in Development only (every token then dies when the app
    /// restarts, which is harmless there) and refuses to start anywhere else.
    /// </summary>
    public static JwtSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Jwt:Key"];
        byte[] key;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            key = Encoding.UTF8.GetBytes(configured);
            if (key.Length < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");
        }
        else if (environment.IsDevelopment())
        {
            key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it with: dotnet user-secrets set Jwt:Key <32+ random characters>");
        }

        return new JwtSettings { SigningKey = new SymmetricSecurityKey(key) };
    }
}

/// <summary>Creates the API's tokens, and re-checks the account behind one on every request.</summary>
public static class JwtTokens
{
    /// <summary>The scheme name of the API's bearer authentication, used by [Authorize]/RequireAuthorization on /api.</summary>
    public const string Scheme = JwtBearerDefaults.AuthenticationScheme;

    // Short claim names, as in the JWT standard (sub, name, email) - with
    // MapInboundClaims off in Program.cs they stay exactly as written here.
    public const string SubjectClaim = "sub";
    public const string NameClaim = "name";
    public const string EmailClaim = "email";
    public const string RoleClaim = "role";
    public const string StampClaim = "stamp";

    /// <summary>
    /// A signed token for <paramref name="user"/>: who they are, their role,
    /// and the account's security stamp (so changing the password makes every
    /// token issued before it stop working - see <see cref="ValidateAccountAsync"/>).
    /// Only ever called for someone who has already completed the full
    /// website login (email link, and the authenticator code if they have one).
    /// </summary>
    public static (string Token, DateTimeOffset ExpiresAt) Create(
        ApplicationUser user, JwtSettings settings, DateTimeOffset now)
    {
        var expires = now + JwtSettings.Lifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtSettings.Issuer,
            Audience = JwtSettings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(SubjectClaim, user.UserId.ToString()),
                new Claim(NameClaim, user.DisplayName),
                new Claim(EmailClaim, user.Email),
                new Claim(RoleClaim, user.Role.ToString()),
                new Claim(StampClaim, user.SecurityStamp)
            ]),
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    /// <summary>
    /// Runs after the signature and expiry are accepted. A valid token alone
    /// would keep working until it expires whatever happened to the account,
    /// so the account is looked up again - deactivated, deleted, or password
    /// changed since the token was issued, and the token is refused. The role
    /// is taken from the database too, so a demoted Admin loses /api/admin at
    /// once. Same idea as AuthCookiePrincipal.RevalidateAsync for the cookie.
    /// </summary>
    public static Task ValidateAccountAsync(TokenValidatedContext context)
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<IUserAccountStore>();
        var principal = context.Principal;

        var user = int.TryParse(principal?.FindFirstValue(SubjectClaim), out var userId)
            ? users.FindById(userId)
            : null;

        if (user is null || !user.IsActive
            || !string.Equals(principal!.FindFirstValue(StampClaim), user.SecurityStamp, StringComparison.Ordinal)
            || principal.FindFirstValue(RoleClaim) != user.Role.ToString())
        {
            context.Fail("The account behind this token has changed.");
        }
        return Task.CompletedTask;
    }
}
