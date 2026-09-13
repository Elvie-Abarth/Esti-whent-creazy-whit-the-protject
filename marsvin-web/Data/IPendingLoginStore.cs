namespace MarsvinWebExample.Data;

public sealed record PendingLoginTicket(int UserId, string? ReturnUrl);

/// <summary>
/// Short-lived, single-use tokens for the email login-confirmation step (see
/// LoginModel/ConfirmLoginModel): a raw token is emailed to the user and only
/// its hash is ever stored, the same way a password never is - a leaked
/// database can't be turned into working login links.
/// </summary>
public interface IPendingLoginStore
{
    /// <summary>Replaces any existing pending login for this user and returns the raw token to email.</summary>
    string Create(int userId, string? returnUrl, TimeSpan validFor);

    /// <summary>Looks up and consumes (deletes) a still-valid token. Null if it's missing, already used, or expired.</summary>
    PendingLoginTicket? Consume(string rawToken);
}
