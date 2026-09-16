using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlPendingLoginStore(string connectionString) : IPendingLoginStore
{
    public string Create(int userId, string? returnUrl, TimeSpan validFor)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Hash(rawToken);

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        // One pending login per user at a time - a fresh login attempt invalidates
        // any earlier unconfirmed one rather than leaving multiple valid links around.
        using (var deleteCommand = new SqlCommand(
            "DELETE FROM dbo.PendingLogins WHERE UserId = @UserId;", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("@UserId", userId);
            deleteCommand.ExecuteNonQuery();
        }

        using (var insertCommand = new SqlCommand(
            "INSERT INTO dbo.PendingLogins (UserId, TokenHash, ReturnUrl, ExpiresAt) " +
            "VALUES (@UserId, @TokenHash, @ReturnUrl, @ExpiresAt);", connection, transaction))
        {
            insertCommand.Parameters.AddWithValue("@UserId", userId);
            insertCommand.Parameters.AddWithValue("@TokenHash", tokenHash);
            insertCommand.Parameters.AddWithValue("@ReturnUrl", (object?)returnUrl ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("@ExpiresAt", DateTime.UtcNow.Add(validFor));
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return rawToken;
    }

    public PendingLoginTicket? Consume(string rawToken)
    {
        var tokenHash = Hash(rawToken);

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        int? userId = null;
        string? returnUrl = null;

        // The reader has to be fully disposed (block-scoped here) before this
        // connection can run another command - MARS isn't enabled, so a still-open
        // SqlDataReader blocks even transaction.Commit() on the same connection.
        using (var selectCommand = new SqlCommand(
            "SELECT UserId, ReturnUrl FROM dbo.PendingLogins " +
            "WHERE TokenHash = @TokenHash AND ExpiresAt > SYSUTCDATETIME();", connection, transaction))
        {
            selectCommand.Parameters.AddWithValue("@TokenHash", tokenHash);
            using var reader = selectCommand.ExecuteReader();
            if (reader.Read())
            {
                userId = reader.GetInt32(reader.GetOrdinal("UserId"));
                var returnUrlOrdinal = reader.GetOrdinal("ReturnUrl");
                returnUrl = reader.IsDBNull(returnUrlOrdinal) ? null : reader.GetString(returnUrlOrdinal);
            }
        }

        if (userId is null)
        {
            transaction.Commit();
            return null;
        }

        // Single-use: whether or not it's already expired by the time someone
        // else looks it up, it's gone the moment it's been read once.
        using (var deleteCommand = new SqlCommand(
            "DELETE FROM dbo.PendingLogins WHERE TokenHash = @TokenHash;", connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("@TokenHash", tokenHash);
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return new PendingLoginTicket(userId.Value, returnUrl);
    }

    public bool IsValid(string rawToken)
    {
        var tokenHash = Hash(rawToken);

        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.PendingLogins WHERE TokenHash = @TokenHash AND ExpiresAt > SYSUTCDATETIME();",
            connection);
        command.Parameters.AddWithValue("@TokenHash", tokenHash);
        return (int)command.ExecuteScalar()! > 0;
    }

    public PendingLoginTicket? Peek(string rawToken)
    {
        var tokenHash = Hash(rawToken);

        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT UserId, ReturnUrl FROM dbo.PendingLogins " +
            "WHERE TokenHash = @TokenHash AND ExpiresAt > SYSUTCDATETIME();", connection);
        command.Parameters.AddWithValue("@TokenHash", tokenHash);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        var returnUrlOrdinal = reader.GetOrdinal("ReturnUrl");
        return new PendingLoginTicket(
            reader.GetInt32(reader.GetOrdinal("UserId")),
            reader.IsDBNull(returnUrlOrdinal) ? null : reader.GetString(returnUrlOrdinal));
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
