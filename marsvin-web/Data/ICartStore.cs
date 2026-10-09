using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// A shopping cart. Two implementations, picked per request in Program.cs:
/// SqlCartStore for a signed-in customer (rows in dbo.CartItems) and
/// SessionCartStore for a guest (kept in the server-side session). Pages only
/// ever see this interface, so they work the same either way.
/// The userId is the signed-in customer's id; the guest implementation
/// ignores it.
/// </summary>
public interface ICartStore
{
    IReadOnlyList<CartLine> GetLines(int userId);
    /// <summary>Adds the product, or raises the quantity if it is already in the cart - never a second line for the same product.</summary>
    void AddOrIncrement(int userId, int productId, int quantity);
    /// <summary>Sets the quantity outright. Zero or less removes the line.</summary>
    void SetQuantity(int userId, int productId, int quantity);
    void RemoveLine(int userId, int productId);
    void Clear(int userId);
}
