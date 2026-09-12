using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlUserAccountStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

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
    public void GetAll_IncludesTheSeededAdminAndEmployee()
    {
        var all = _users.GetAll();

        Assert.Contains(all, u => u.Email == "admin@marsvin.dk" && u.Role == UserRole.Admin);
        Assert.Contains(all, u => u.Email == "employee@marsvin.dk" && u.Role == UserRole.Employee);
    }
}
