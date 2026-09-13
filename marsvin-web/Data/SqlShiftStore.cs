using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlShiftStore(string connectionString) : IShiftStore
{
    private const string SelectColumns =
        "SELECT s.ShiftId, s.UserId, u.DisplayName, s.StartAt, s.EndAt, s.Note " +
        "FROM dbo.Shifts s JOIN dbo.Users u ON u.UserId = s.UserId ";

    public IReadOnlyList<Shift> GetAll()
    {
        using var connection = Open();
        using var command = new SqlCommand(SelectColumns + "ORDER BY s.StartAt;", connection);

        var shifts = new List<Shift>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) shifts.Add(ReadShift(reader));
        return shifts;
    }

    public IReadOnlyList<Shift> GetForUser(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            SelectColumns + "WHERE s.UserId = @UserId ORDER BY s.StartAt;", connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var shifts = new List<Shift>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) shifts.Add(ReadShift(reader));
        return shifts;
    }

    public Shift? GetById(int shiftId)
    {
        using var connection = Open();
        using var command = new SqlCommand(SelectColumns + "WHERE s.ShiftId = @ShiftId;", connection);
        command.Parameters.AddWithValue("@ShiftId", shiftId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadShift(reader) : null;
    }

    public void Create(int userId, DateTime startAt, DateTime endAt, string? note)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "INSERT INTO dbo.Shifts (UserId, StartAt, EndAt, Note) VALUES (@UserId, @StartAt, @EndAt, @Note);",
            connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StartAt", startAt);
        command.Parameters.AddWithValue("@EndAt", endAt);
        command.Parameters.AddWithValue("@Note", (object?)note ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void Delete(int shiftId)
    {
        using var connection = Open();
        using var command = new SqlCommand("DELETE FROM dbo.Shifts WHERE ShiftId = @ShiftId;", connection);
        command.Parameters.AddWithValue("@ShiftId", shiftId);
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static Shift ReadShift(SqlDataReader reader) => new()
    {
        ShiftId = reader.GetInt32(reader.GetOrdinal("ShiftId")),
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        StaffName = reader.GetString(reader.GetOrdinal("DisplayName")),
        StartAt = reader.GetDateTime(reader.GetOrdinal("StartAt")),
        EndAt = reader.GetDateTime(reader.GetOrdinal("EndAt")),
        Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note"))
    };
}
