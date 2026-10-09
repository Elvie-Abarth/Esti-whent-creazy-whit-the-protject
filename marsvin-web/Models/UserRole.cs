namespace MarsvinWebExample.Models;

/// <summary>
/// The three roles an account can have - exactly one each. Stored as a number
/// on dbo.Users.Role (0, 1, 2 in this order), and what every
/// [Authorize(Roles = "...")] in the project is checked against.
/// Customers register themselves; Employee and Admin accounts are created by
/// an Admin on /Admin/Users (or seeded for the demo).
/// </summary>
public enum UserRole
{
    Customer,
    Employee,
    Admin
}
