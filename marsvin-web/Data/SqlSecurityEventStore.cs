using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// Security events in SQL Server (dbo.SecurityEvents). Plain ADO.NET,
/// parameterised like every other store - the type filters below build a
/// parameterised <c>IN (@t0, @t1, ...)</c> list from an enum, never from any
/// text a visitor could influence.
/// </summary>
public sealed class SqlSecurityEventStore(string connectionString) : ISecurityEventStore
{
    public void Record(SecurityEvent e)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.SecurityEvents (Type, Email, IpAddress, Path, Detail)
            VALUES (@Type, @Email, @IpAddress, @Path, @Detail);
            """, connection);
        command.Parameters.AddWithValue("@Type", (byte)e.Type);
        command.Parameters.AddWithValue("@Email", (object?)Trim(e.Email, 256) ?? DBNull.Value);
        command.Parameters.AddWithValue("@IpAddress", (object?)Trim(e.IpAddress, 45) ?? DBNull.Value);
        command.Parameters.AddWithValue("@Path", (object?)Trim(e.Path, 400) ?? DBNull.Value);
        command.Parameters.AddWithValue("@Detail", (object?)Trim(e.Detail, 500) ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SecurityEvent> GetRecent(int count)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT TOP (@Count) SecurityEventId, CreatedAt, Type, Email, IpAddress, Path, Detail " +
            "FROM dbo.SecurityEvents ORDER BY CreatedAt DESC, SecurityEventId DESC;", connection);
        command.Parameters.AddWithValue("@Count", count);

        var events = new List<SecurityEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new SecurityEvent
            {
                SecurityEventId = reader.GetInt64(reader.GetOrdinal("SecurityEventId")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Type = (SecurityEventType)reader.GetByte(reader.GetOrdinal("Type")),
                Email = reader.GetNullable("Email"),
                IpAddress = reader.GetNullable("IpAddress"),
                Path = reader.GetNullable("Path"),
                Detail = reader.GetNullable("Detail")
            });
        }
        return events;
    }

    public int CountSince(DateTime since, params SecurityEventType[] types)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.SecurityEvents WHERE CreatedAt >= @Since" + TypeFilter(types) + ";", connection);
        command.Parameters.AddWithValue("@Since", since);
        AddTypeParameters(command, types);
        return (int)command.ExecuteScalar()!;
    }

    public int CountFromIpSince(string ipAddress, DateTime since, params SecurityEventType[] types)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.SecurityEvents WHERE IpAddress = @Ip AND CreatedAt >= @Since" + TypeFilter(types) + ";", connection);
        command.Parameters.AddWithValue("@Ip", ipAddress);
        command.Parameters.AddWithValue("@Since", since);
        AddTypeParameters(command, types);
        return (int)command.ExecuteScalar()!;
    }

    public int DistinctEmailsFromIpSince(string ipAddress, DateTime since, SecurityEventType type)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT COUNT(DISTINCT Email) FROM dbo.SecurityEvents " +
            "WHERE IpAddress = @Ip AND CreatedAt >= @Since AND Type = @Type AND Email IS NOT NULL;", connection);
        command.Parameters.AddWithValue("@Ip", ipAddress);
        command.Parameters.AddWithValue("@Since", since);
        command.Parameters.AddWithValue("@Type", (byte)type);
        return (int)command.ExecuteScalar()!;
    }

    public int DeleteOlderThan(DateTime cutoff)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "DELETE FROM dbo.SecurityEvents WHERE CreatedAt < @Cutoff;", connection);
        command.Parameters.AddWithValue("@Cutoff", cutoff);
        return command.ExecuteNonQuery();
    }

    // Builds " AND Type IN (@t0, @t1, ...)" from the enum values - the SQL text
    // only ever gets parameter placeholders, never the values themselves.
    private static string TypeFilter(SecurityEventType[] types) =>
        types.Length == 0 ? "" : " AND Type IN (" + string.Join(", ", types.Select((_, i) => "@t" + i)) + ")";

    private static void AddTypeParameters(SqlCommand command, SecurityEventType[] types)
    {
        for (var i = 0; i < types.Length; i++)
            command.Parameters.AddWithValue("@t" + i, (byte)types[i]);
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : (value.Length <= max ? value : value[..max]);

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}

file static class SecurityEventReaderExtensions
{
    public static string? GetNullable(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
