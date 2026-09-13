using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public interface IUserAccountStore
{
    ApplicationUser? FindByEmail(string email);
    ApplicationUser? FindById(int userId);
    IReadOnlyList<ApplicationUser> GetAll();

    /// <summary>Creates a user. Returns false if the email is already taken.</summary>
    bool CreateUser(string email, string passwordHash, string displayName, UserRole role);

    void UpdateRole(int userId, UserRole role);
    void SetActive(int userId, bool isActive);

    /// <summary>How many Admin accounts are currently active - used to block removing the last one (demoting, deactivating, or deleting).</summary>
    int CountActiveAdmins();

    /// <summary>Self-service profile update. Returns false if the email is already used by a different account.</summary>
    bool UpdateProfile(int userId, string displayName, string email);

    void UpdatePassword(int userId, string passwordHash);

    /// <summary>
    /// Permanently deletes the account. Their cart (if any) is deleted with it, but
    /// past orders are kept as a historical record - just orphaned (Order.UserId set
    /// to null) rather than deleted, so sales history survives an account being removed.
    /// </summary>
    void DeleteUser(int userId);

    /// <summary>
    /// Marks the account as active right now and clears any pending inactivity-warning
    /// stage - called once a login is fully confirmed (see ConfirmLoginModel), not just
    /// password-verified. See Privatliv.cshtml for the retention policy this supports.
    /// </summary>
    void RecordActivity(int userId);

    /// <summary>
    /// Permanently deletes every Customer account (never Employee/Admin) whose
    /// LastActiveAt is older than the cutoff, the same way DeleteUser does (cart
    /// cleared, orders orphaned rather than deleted). Returns the deleted
    /// accounts' emails, for logging.
    /// </summary>
    IReadOnlyList<string> DeleteInactiveCustomers(DateTime cutoff);

    /// <summary>
    /// Customer accounts that haven't logged in since <paramref name="lastActiveBefore"/>
    /// and haven't already had this warning stage (or a later one) sent - see
    /// InactiveAccountCleanupService. Stage 1 is the "2 months left" email, stage 2 is
    /// "1 month left", so each account gets every applicable warning exactly once.
    /// </summary>
    IReadOnlyList<ApplicationUser> GetCustomersNeedingInactivityWarning(DateTime lastActiveBefore, byte stage);

    void SetInactivityWarningStage(int userId, byte stage);
}
