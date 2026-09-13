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

    /// <summary>
    /// Permanently deletes the account. Their cart (if any) is deleted with it, but
    /// past orders are kept as a historical record - just orphaned (Order.UserId set
    /// to null) rather than deleted, so sales history survives an account being removed.
    /// </summary>
    void DeleteUser(int userId);
}
