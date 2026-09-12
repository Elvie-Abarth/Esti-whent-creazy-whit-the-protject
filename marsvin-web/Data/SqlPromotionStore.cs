using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlPromotionStore(string connectionString) : IPromotionStore
{
    public IReadOnlyList<Promotion> GetAll()
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT PromotionId, Title, Description, DiscountPercent, ProductId, StartDate, EndDate, IsActive " +
            "FROM dbo.Promotions ORDER BY StartDate DESC;", connection);

        var promotions = new List<Promotion>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) promotions.Add(ReadPromotion(reader));
        return promotions;
    }

    public Promotion? FindById(int id)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT PromotionId, Title, Description, DiscountPercent, ProductId, StartDate, EndDate, IsActive " +
            "FROM dbo.Promotions WHERE PromotionId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPromotion(reader) : null;
    }

    public void Create(Promotion promotion)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Promotions (Title, Description, DiscountPercent, ProductId, StartDate, EndDate, IsActive)
            VALUES (@Title, @Description, @DiscountPercent, @ProductId, @StartDate, @EndDate, @IsActive);
            """, connection);
        BindPromotion(command, promotion);
        command.ExecuteNonQuery();
    }

    public void Update(Promotion promotion)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            UPDATE dbo.Promotions SET
                Title = @Title, Description = @Description, DiscountPercent = @DiscountPercent,
                ProductId = @ProductId, StartDate = @StartDate, EndDate = @EndDate, IsActive = @IsActive
            WHERE PromotionId = @PromotionId;
            """, connection);
        BindPromotion(command, promotion);
        command.Parameters.AddWithValue("@PromotionId", promotion.PromotionId);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = Open();
        using var command = new SqlCommand("DELETE FROM dbo.Promotions WHERE PromotionId = @Id;", connection);
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
    }

    private static void BindPromotion(SqlCommand command, Promotion promotion)
    {
        command.Parameters.AddWithValue("@Title", promotion.Title);
        command.Parameters.AddWithValue("@Description", promotion.Description);
        command.Parameters.AddWithValue("@DiscountPercent", promotion.DiscountPercent);
        command.Parameters.AddWithValue("@ProductId", (object?)promotion.ProductId ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartDate", promotion.StartDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@EndDate", promotion.EndDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@IsActive", promotion.IsActive);
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static Promotion ReadPromotion(SqlDataReader reader) => new()
    {
        PromotionId = reader.GetInt32(reader.GetOrdinal("PromotionId")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Description = reader.GetString(reader.GetOrdinal("Description")),
        DiscountPercent = reader.GetInt32(reader.GetOrdinal("DiscountPercent")),
        ProductId = reader.IsDBNull(reader.GetOrdinal("ProductId")) ? null : reader.GetInt32(reader.GetOrdinal("ProductId")),
        StartDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("StartDate"))),
        EndDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("EndDate"))),
        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
    };
}
