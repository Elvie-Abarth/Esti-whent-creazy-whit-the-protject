using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlUserAccountStore(string connectionString) : IUserAccountStore
{
    public ApplicationUser? FindByEmail(string email)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT UserId, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt, LastActiveAt " +
            "FROM dbo.Users WHERE Email = @Email;", connection);
        command.Parameters.AddWithValue("@Email", email);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public ApplicationUser? FindById(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT UserId, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt, LastActiveAt " +
            "FROM dbo.Users WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public IReadOnlyList<ApplicationUser> GetAll()
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT UserId, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt, LastActiveAt " +
            "FROM dbo.Users ORDER BY UserId;", connection);

        var users = new List<ApplicationUser>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) users.Add(ReadUser(reader));
        return users;
    }

    public bool CreateUser(string email, string passwordHash, string displayName, UserRole role)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
            BEGIN
                INSERT INTO dbo.Users (Email, PasswordHash, DisplayName, Role, IsActive)
                VALUES (@Email, @PasswordHash, @DisplayName, @Role, 1);
                SELECT CAST(1 AS BIT);
            END
            ELSE
                SELECT CAST(0 AS BIT);
            """, connection);
        command.Parameters.AddWithValue("@Email", email);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@Role", (byte)role);

        return (bool)command.ExecuteScalar()!;
    }

    public void UpdateRole(int userId, UserRole role)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET Role = @Role WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@Role", (byte)role);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    public void SetActive(int userId, bool isActive)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET IsActive = @IsActive WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@IsActive", isActive);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    public bool UpdateProfile(int userId, string displayName, string email)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND UserId <> @UserId)
            BEGIN
                UPDATE dbo.Users SET DisplayName = @DisplayName, Email = @Email WHERE UserId = @UserId;
                SELECT CAST(1 AS BIT);
            END
            ELSE
                SELECT CAST(0 AS BIT);
            """, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@Email", email);

        return (bool)command.ExecuteScalar()!;
    }

    public void UpdatePassword(int userId, string passwordHash)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET PasswordHash = @PasswordHash WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    public void DeleteUser(int userId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var cartCommand = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId;", connection, transaction))
        {
            cartCommand.Parameters.AddWithValue("@UserId", userId);
            cartCommand.ExecuteNonQuery();
        }

        // Shifts and TimeOffRequests both have a real FK to Users (unlike
        // Orders, which orphan instead - a departed staff member's schedule
        // isn't a record worth keeping the way a sales order is), so both
        // have to go before the Users row does.
        using (var shiftsCommand = new SqlCommand(
            "DELETE FROM dbo.Shifts WHERE UserId = @UserId;", connection, transaction))
        {
            shiftsCommand.Parameters.AddWithValue("@UserId", userId);
            shiftsCommand.ExecuteNonQuery();
        }

        using (var timeOffCommand = new SqlCommand(
            "DELETE FROM dbo.TimeOffRequests WHERE UserId = @UserId;", connection, transaction))
        {
            timeOffCommand.Parameters.AddWithValue("@UserId", userId);
            timeOffCommand.ExecuteNonQuery();
        }

        using (var ordersCommand = new SqlCommand(
            "UPDATE dbo.Orders SET UserId = NULL WHERE UserId = @UserId;", connection, transaction))
        {
            ordersCommand.Parameters.AddWithValue("@UserId", userId);
            ordersCommand.ExecuteNonQuery();
        }

        using (var userCommand = new SqlCommand(
            "DELETE FROM dbo.Users WHERE UserId = @UserId;", connection, transaction))
        {
            userCommand.Parameters.AddWithValue("@UserId", userId);
            userCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public int CountActiveAdmins()
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Users WHERE Role = @AdminRole AND IsActive = 1;", connection);
        command.Parameters.AddWithValue("@AdminRole", (byte)UserRole.Admin);
        return (int)command.ExecuteScalar()!;
    }

    public void RecordActivity(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET LastActiveAt = SYSUTCDATETIME(), InactivityWarningStage = 0 WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> DeleteInactiveCustomers(DateTime cutoff)
    {
        var toDelete = new List<(int UserId, string Email)>();
        using (var connection = Open())
        using (var command = new SqlCommand(
            "SELECT UserId, Email FROM dbo.Users WHERE Role = @CustomerRole AND LastActiveAt < @Cutoff;", connection))
        {
            command.Parameters.AddWithValue("@CustomerRole", (byte)UserRole.Customer);
            command.Parameters.AddWithValue("@Cutoff", cutoff);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                toDelete.Add((reader.GetInt32(reader.GetOrdinal("UserId")), reader.GetString(reader.GetOrdinal("Email"))));
            }
        }

        // Same transaction DeleteUser already uses per account (cart cleared,
        // orders orphaned not destroyed) - reused rather than duplicated, so a
        // manual admin delete and this automatic one can never drift apart.
        foreach (var (userId, _) in toDelete)
        {
            DeleteUser(userId);
        }

        return toDelete.Select(u => u.Email).ToList();
    }

    public IReadOnlyList<ApplicationUser> GetCustomersNeedingInactivityWarning(DateTime lastActiveBefore, byte stage)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT UserId, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt, LastActiveAt " +
            "FROM dbo.Users WHERE Role = @CustomerRole AND LastActiveAt <= @LastActiveBefore AND InactivityWarningStage < @Stage;",
            connection);
        command.Parameters.AddWithValue("@CustomerRole", (byte)UserRole.Customer);
        command.Parameters.AddWithValue("@LastActiveBefore", lastActiveBefore);
        command.Parameters.AddWithValue("@Stage", stage);

        var users = new List<ApplicationUser>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) users.Add(ReadUser(reader));
        return users;
    }

    public void SetInactivityWarningStage(int userId, byte stage)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET InactivityWarningStage = @Stage WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@Stage", stage);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static ApplicationUser ReadUser(SqlDataReader reader) => new()
    {
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        Email = reader.GetString(reader.GetOrdinal("Email")),
        PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
        DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
        Role = (UserRole)reader.GetByte(reader.GetOrdinal("Role")),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        LastActiveAt = reader.GetDateTime(reader.GetOrdinal("LastActiveAt"))
    };
}
