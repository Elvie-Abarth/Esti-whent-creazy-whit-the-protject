using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests;

/// <summary>
/// In-memory stand-in for SessionCartStore, for unit-testing a guest
/// checkout's PageModel logic in isolation. SqlCartStore can't fill this
/// role for a guest the way it does for a signed-in customer (see other
/// PaymentModelTests) - CartItems.UserId is a real FK to Users, and a guest
/// (by definition) has no Users row at all for that FK to point at. The int
/// userId every ICartStore method takes is ignored here too, same as the
/// real SessionCartStore it stands in for.
/// </summary>
internal sealed class FakeCartStore : ICartStore
{
    private readonly List<CartLine> _lines = [];

    public IReadOnlyList<CartLine> GetLines(int userId) => _lines;

    public void AddOrIncrement(int userId, int productId, int quantity)
    {
        var index = _lines.FindIndex(l => l.ProductId == productId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"FakeCartStore has no catalog to resolve product {productId} from - call Add(CartLine) instead.");
        }
        var existing = _lines[index];
        _lines[index] = new CartLine
        {
            ProductId = existing.ProductId,
            ProductName = existing.ProductName,
            ProductNameEn = existing.ProductNameEn,
            UnitPrice = existing.UnitPrice,
            Quantity = existing.Quantity + quantity,
            IsAnimal = existing.IsAnimal,
            PhotoUrl = existing.PhotoUrl
        };
    }

    /// <summary>Test setup helper - adds a fully-formed line directly, since this fake has no ICatalog to resolve one from a bare productId.</summary>
    public void Add(CartLine line) => _lines.Add(line);

    public void SetQuantity(int userId, int productId, int quantity) =>
        throw new NotSupportedException("Not needed by the guest-checkout tests this fake exists for.");

    public void RemoveLine(int userId, int productId) =>
        throw new NotSupportedException("Not needed by the guest-checkout tests this fake exists for.");

    public void Clear(int userId) => _lines.Clear();
}
