using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlAuditLogStore(string connectionString) : IAuditLogStore
{
    public void Record(int? actorUserId, string actorName, string action, string details)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.AuditLog (ActorUserId, ActorName, Action, Details)
            VALUES (@ActorUserId, @ActorName, @Action, @Details);
            """, connection);
        command.Parameters.AddWithValue("@ActorUserId", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ActorName", actorName);
        command.Parameters.AddWithValue("@Action", action);
        command.Parameters.AddWithValue("@Details", details);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditLogEntry> GetRecent(int count)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT TOP (@Count) AuditLogId, ActorUserId, ActorName, Action, Details, CreatedAt " +
            "FROM dbo.AuditLog ORDER BY CreatedAt DESC, AuditLogId DESC;", connection);
        command.Parameters.AddWithValue("@Count", count);

        var entries = new List<AuditLogEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var actorUserIdOrdinal = reader.GetOrdinal("ActorUserId");
            entries.Add(new AuditLogEntry
            {
                AuditLogId = reader.GetInt32(reader.GetOrdinal("AuditLogId")),
                ActorUserId = reader.IsDBNull(actorUserIdOrdinal) ? null : reader.GetInt32(actorUserIdOrdinal),
                ActorName = reader.GetString(reader.GetOrdinal("ActorName")),
                Action = reader.GetString(reader.GetOrdinal("Action")),
                Details = reader.GetString(reader.GetOrdinal("Details")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return entries;
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
