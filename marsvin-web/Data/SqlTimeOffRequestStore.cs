using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlTimeOffRequestStore(string connectionString) : ITimeOffRequestStore
{
    private const string SelectColumns =
        "SELECT r.RequestId, r.UserId, u.DisplayName, r.StartDate, r.EndDate, r.Reason, " +
        "r.Status, r.RequestedAt, r.DecidedAt, r.DecidedByName " +
        "FROM dbo.TimeOffRequests r JOIN dbo.Users u ON u.UserId = r.UserId ";

    public IReadOnlyList<TimeOffRequest> GetForUser(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            SelectColumns + "WHERE r.UserId = @UserId ORDER BY r.RequestedAt DESC;", connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var requests = new List<TimeOffRequest>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) requests.Add(ReadRequest(reader));
        return requests;
    }

    public IReadOnlyList<TimeOffRequest> GetAll()
    {
        using var connection = Open();
        using var command = new SqlCommand(SelectColumns + "ORDER BY r.RequestedAt DESC;", connection);

        var requests = new List<TimeOffRequest>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) requests.Add(ReadRequest(reader));
        return requests;
    }

    public void Create(int userId, DateOnly startDate, DateOnly endDate, string? reason)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "INSERT INTO dbo.TimeOffRequests (UserId, StartDate, EndDate, Reason) " +
            "VALUES (@UserId, @StartDate, @EndDate, @Reason);", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartDate", startDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@EndDate", endDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@Reason", (object?)reason ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Decide(int requestId, TimeOffStatus status, string decidedByName)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "UPDATE dbo.TimeOffRequests SET Status = @Status, DecidedAt = SYSUTCDATETIME(), DecidedByName = @DecidedByName " +
            "WHERE RequestId = @RequestId;", connection);
        command.Parameters.AddWithValue("@Status", (byte)status);
        command.Parameters.AddWithValue("@DecidedByName", decidedByName);
        command.Parameters.AddWithValue("@RequestId", requestId);
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static TimeOffRequest ReadRequest(SqlDataReader reader) => new()
    {
        RequestId = reader.GetInt32(reader.GetOrdinal("RequestId")),
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        StaffName = reader.GetString(reader.GetOrdinal("DisplayName")),
        StartDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("StartDate"))),
        EndDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("EndDate"))),
        Reason = reader.IsDBNull(reader.GetOrdinal("Reason")) ? null : reader.GetString(reader.GetOrdinal("Reason")),
        Status = (TimeOffStatus)reader.GetByte(reader.GetOrdinal("Status")),
        RequestedAt = reader.GetDateTime(reader.GetOrdinal("RequestedAt")),
        DecidedAt = reader.IsDBNull(reader.GetOrdinal("DecidedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("DecidedAt")),
        DecidedByName = reader.IsDBNull(reader.GetOrdinal("DecidedByName")) ? null : reader.GetString(reader.GetOrdinal("DecidedByName"))
    };
}
