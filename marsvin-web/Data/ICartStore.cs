using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

public interface ICartStore
{
    IReadOnlyList<CartLine> GetLines(int userId);
    void AddOrIncrement(int userId, int productId, int quantity);
    void RemoveLine(int userId, int productId);
    void Clear(int userId);
}
