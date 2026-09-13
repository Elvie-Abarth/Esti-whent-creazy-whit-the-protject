using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlUserAccountStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);

    private int? ReadOrderUserId(int orderId)
    {
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = new SqlCommand(
            "SELECT UserId FROM dbo.Orders WHERE OrderId = @OrderId;", connection);
        command.Parameters.AddWithValue("@OrderId", orderId);
        var value = command.ExecuteScalar();
        return value is DBNull or null ? null : (int)value;
    }

    // No public API sets LastActiveAt to an arbitrary point in the past (by
    // design - the only writer is RecordActivity, which always means "now").
    // Tests for the 2-year cutoff have to reach past that on purpose.
    private void BackdateLastActiveAt(int userId, DateTime lastActiveAt)
    {
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET LastActiveAt = @LastActiveAt WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@LastActiveAt", lastActiveAt);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void CreateUser_ThenFindByEmail_RoundTrips()
    {
        var email = $"round-trip-{Guid.NewGuid():N}@example.com";

        var created = _users.CreateUser(email, "hash", "Round Trip", UserRole.Customer);
        var found = _users.FindByEmail(email);

        Assert.True(created);
        Assert.NotNull(found);
        Assert.Equal(email, found!.Email);
        Assert.Equal("Round Trip", found.DisplayName);
        Assert.Equal(UserRole.Customer, found.Role);
        Assert.True(found.IsActive);
    }

    [Fact]
    public void CreateUser_DuplicateEmail_ReturnsFalseAndDoesNotOverwrite()
    {
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "first-hash", "First", UserRole.Customer);

        var secondCreated = _users.CreateUser(email, "second-hash", "Second", UserRole.Admin);

        Assert.False(secondCreated);
        var found = _users.FindByEmail(email);
        Assert.Equal("First", found!.DisplayName);
        Assert.Equal("first-hash", found.PasswordHash);
    }

    [Fact]
    public void FindByEmail_UnknownEmail_ReturnsNull()
    {
        Assert.Null(_users.FindByEmail($"nobody-{Guid.NewGuid():N}@example.com"));
    }

    [Fact]
    public void FindById_MatchesFindByEmail()
    {
        var email = $"by-id-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "By Id", UserRole.Employee);
        var byEmail = _users.FindByEmail(email)!;

        var byId = _users.FindById(byEmail.UserId);

        Assert.NotNull(byId);
        Assert.Equal(email, byId!.Email);
    }

    [Fact]
    public void UpdateRole_ChangesStoredRole()
    {
        var email = $"role-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Role Test", UserRole.Customer);
        var user = _users.FindByEmail(email)!;

        _users.UpdateRole(user.UserId, UserRole.Admin);

        Assert.Equal(UserRole.Admin, _users.FindById(user.UserId)!.Role);
    }

    [Fact]
    public void SetActive_ChangesStoredFlag()
    {
        var email = $"active-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Active Test", UserRole.Customer);
        var user = _users.FindByEmail(email)!;

        _users.SetActive(user.UserId, false);

        Assert.False(_users.FindById(user.UserId)!.IsActive);
    }

    [Fact]
    public void DeleteUser_WithNoOrders_RemovesTheAccount()
    {
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Delete Me", UserRole.Customer);
        var user = _users.FindByEmail(email)!;

        _users.DeleteUser(user.UserId);

        Assert.Null(_users.FindById(user.UserId));
    }

    [Fact]
    public void DeleteUser_WithPastOrders_KeepsTheOrderButOrphansIt()
    {
        var email = $"delete-with-order-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Delete With Order", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        _cart.AddOrIncrement(user.UserId, 104, 1);
        var order = _orders.Checkout(user.UserId).Order!;

        _users.DeleteUser(user.UserId);

        Assert.Null(_users.FindById(user.UserId));
        Assert.Null(ReadOrderUserId(order.OrderId)); // order row itself still exists - only the link is gone
    }

    [Fact]
    public void DeleteUser_WithAnUnfinishedCart_RemovesTheCartToo()
    {
        var email = $"delete-with-cart-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Delete With Cart", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        _cart.AddOrIncrement(user.UserId, 104, 1);

        _users.DeleteUser(user.UserId);

        Assert.Empty(_cart.GetLines(user.UserId));
    }

    [Fact]
    public void RecordActivity_SetsLastActiveAtToNow()
    {
        var email = $"activity-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Activity Test", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        BackdateLastActiveAt(user.UserId, DateTime.UtcNow.AddYears(-3));

        _users.RecordActivity(user.UserId);

        var refreshed = _users.FindById(user.UserId)!;
        Assert.True(refreshed.LastActiveAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void DeleteInactiveCustomers_InactiveOverTwoYears_IsDeleted()
    {
        var email = $"stale-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Stale Customer", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        BackdateLastActiveAt(user.UserId, DateTime.UtcNow.AddYears(-3));
        var cutoff = DateTime.UtcNow - InactiveAccountCleanupService.InactivityThreshold;

        var deletedEmails = _users.DeleteInactiveCustomers(cutoff);

        Assert.Contains(email, deletedEmails);
        Assert.Null(_users.FindById(user.UserId));
    }

    [Fact]
    public void DeleteInactiveCustomers_RecentlyActive_IsNotDeleted()
    {
        var email = $"recent-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Recent Customer", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        var cutoff = DateTime.UtcNow - InactiveAccountCleanupService.InactivityThreshold;

        var deletedEmails = _users.DeleteInactiveCustomers(cutoff);

        Assert.DoesNotContain(email, deletedEmails);
        Assert.NotNull(_users.FindById(user.UserId));
    }

    [Fact]
    public void DeleteInactiveCustomers_NeverTouchesInactiveStaffAccounts()
    {
        var email = $"stale-staff-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Stale Employee", UserRole.Employee);
        var user = _users.FindByEmail(email)!;
        BackdateLastActiveAt(user.UserId, DateTime.UtcNow.AddYears(-3));
        var cutoff = DateTime.UtcNow - InactiveAccountCleanupService.InactivityThreshold;

        var deletedEmails = _users.DeleteInactiveCustomers(cutoff);

        Assert.DoesNotContain(email, deletedEmails);
        Assert.NotNull(_users.FindById(user.UserId));
    }

    [Fact]
    public void DeleteInactiveCustomers_KeepsTheirOrdersOrphanedLikeDeleteUserDoes()
    {
        var email = $"stale-with-order-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Stale With Order", UserRole.Customer);
        var user = _users.FindByEmail(email)!;
        _cart.AddOrIncrement(user.UserId, 104, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        BackdateLastActiveAt(user.UserId, DateTime.UtcNow.AddYears(-3));
        var cutoff = DateTime.UtcNow - InactiveAccountCleanupService.InactivityThreshold;

        _users.DeleteInactiveCustomers(cutoff);

        Assert.Null(_users.FindById(user.UserId));
        Assert.Null(ReadOrderUserId(order.OrderId));
    }

    [Fact]
    public void CountActiveAdmins_CountsOnlyActiveAdmins()
    {
        var before = _users.CountActiveAdmins();
        var email = $"count-admin-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", "Count Admin", UserRole.Admin);
        var created = _users.FindByEmail(email)!;
        Assert.Equal(before + 1, _users.CountActiveAdmins());

        _users.SetActive(created.UserId, false);
        Assert.Equal(before, _users.CountActiveAdmins());

        _users.SetActive(created.UserId, true);
        _users.UpdateRole(created.UserId, UserRole.Employee);
        Assert.Equal(before, _users.CountActiveAdmins());
    }

    [Fact]
    public void GetAll_IncludesTheSeededAdminAndEmployee()
    {
        var all = _users.GetAll();

        Assert.Contains(all, u => u.Email == "admin@marsvin.dk" && u.Role == UserRole.Admin);
        Assert.Contains(all, u => u.Email == "employee@marsvin.dk" && u.Role == UserRole.Employee);
    }
}
