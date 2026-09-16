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

    /// <summary>
    /// True if the token is still valid, without consuming it - lets a GET
    /// (see Account/ResetPassword) tell the visitor up front that a link is
    /// dead, rather than only after they've filled in a form and posted it.
    /// The actual use still goes through Consume, so this never becomes a
    /// second way to redeem the same token.
    /// </summary>
    bool IsValid(string rawToken);

    /// <summary>
    /// Looks up a still-valid token's ticket without consuming it - lets
    /// Account/VerifyTotp check which user a token belongs to on a wrong
    /// code without burning the token, so the user can retry until it
    /// actually expires. The final successful check still calls Consume to
    /// finalize it as single-use.
    /// </summary>
    PendingLoginTicket? Peek(string rawToken);
}
