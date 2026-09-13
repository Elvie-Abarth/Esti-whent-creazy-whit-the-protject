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
    public void GetAll_IncludesTheSeededAdminAndEmployee()
    {
        var all = _users.GetAll();

        Assert.Contains(all, u => u.Email == "admin@marsvin.dk" && u.Role == UserRole.Admin);
        Assert.Contains(all, u => u.Email == "employee@marsvin.dk" && u.Role == UserRole.Employee);
    }
}
