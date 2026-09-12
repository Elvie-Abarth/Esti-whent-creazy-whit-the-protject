using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

public sealed class SqlCartStore(string connectionString) : ICartStore
{
    public IReadOnlyList<CartLine> GetLines(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            SELECT c.ProductId, c.Quantity, p.Name, p.Price,
                   CASE WHEN a.ProductId IS NULL THEN 0 ELSE 1 END AS IsAnimal
            FROM dbo.CartItems c
            JOIN dbo.Products p ON p.ProductId = c.ProductId
            LEFT JOIN dbo.Animals a ON a.ProductId = p.ProductId
            WHERE c.UserId = @UserId
            ORDER BY c.CartItemId;
            """, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var lines = new List<CartLine>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(new CartLine
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                ProductName = reader.GetString(reader.GetOrdinal("Name")),
                UnitPrice = reader.GetDecimal(reader.GetOrdinal("Price")),
                Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
                IsAnimal = reader.GetInt32(reader.GetOrdinal("IsAnimal")) == 1
            });
        }
        return lines;
    }

    public void AddOrIncrement(int userId, int productId, int quantity)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            MERGE dbo.CartItems AS target
            USING (SELECT @UserId AS UserId, @ProductId AS ProductId) AS source
                ON target.UserId = source.UserId AND target.ProductId = source.ProductId
            WHEN MATCHED THEN
                UPDATE SET Quantity = target.Quantity + @Quantity
            WHEN NOT MATCHED THEN
                INSERT (UserId, ProductId, Quantity) VALUES (@UserId, @ProductId, @Quantity);
            """, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.ExecuteNonQuery();
    }

    public void RemoveLine(int userId, int productId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId AND ProductId = @ProductId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.ExecuteNonQuery();
    }

    public void Clear(int userId)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "DELETE FROM dbo.CartItems WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
