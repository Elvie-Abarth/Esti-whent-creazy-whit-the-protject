using System.Text.Json;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Http;

namespace MarsvinWebExample.Data;

/// <summary>
/// The cart for a visitor who isn't logged in - guest checkout. Backed by
/// ASP.NET Core's own session (see Program.cs's AddSession/UseSession),
/// not dbo.CartItems: there's no UserId to key a SQL row on, and nothing
/// here needs to survive longer than the browser session anyway - unlike
/// an authenticated customer's cart, which should still be there days
/// later on a different device.
///
/// Only ever stores (ProductId, Quantity) pairs, never a price or name -
/// everything else about a line is resolved fresh against ICatalog on every
/// read, the same reason SqlCartStore joins against Products/Animals/
/// StockProducts at read time instead of snapshotting them into CartItems:
/// a stale cached price would be wrong the moment an admin changes it.
///
/// The int userId every ICartStore method takes is ignored here entirely -
/// there is no user, just "whoever this session belongs to" - see
/// Program.cs's registration of ICartStore for why callers never need to
/// know which implementation they got.
/// </summary>
public sealed class SessionCartStore(ISession session, ICatalog catalog) : ICartStore
{
    private const string SessionKey = "GuestCart";

    public IReadOnlyList<CartLine> GetLines(int userId)
    {
        var lines = new List<CartLine>();
        foreach (var (productId, quantity) in ReadEntries())
        {
            // A product a guest added earlier in the same session can have
            // since been deleted (or, for an animal, sold to someone else) -
            // silently drop it from what's shown rather than crash; checkout
            // re-validates everything still in the cart at that point anyway.
            var product = catalog.FindProduct(productId);
            if (product is null) continue;

            lines.Add(new CartLine
            {
                ProductId = product.ProductId,
                ProductName = product.Name,
                ProductNameEn = product is StockProduct sp ? sp.NameEn : null,
                UnitPrice = product.Price,
                Quantity = quantity,
                IsAnimal = product is Animal,
                PhotoUrl = product switch
                {
                    Animal a => a.PhotoUrl,
                    StockProduct s => s.PhotoUrl,
                    _ => null
                }
            });
        }
        return lines;
    }

    public void AddOrIncrement(int userId, int productId, int quantity)
    {
        var entries = ReadEntries();
        var index = entries.FindIndex(e => e.ProductId == productId);
        if (index >= 0)
            entries[index] = (productId, entries[index].Quantity + quantity);
        else
            entries.Add((productId, quantity));
        WriteEntries(entries);
    }

    public void SetQuantity(int userId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            RemoveLine(userId, productId);
            return;
        }

        var entries = ReadEntries();
        var index = entries.FindIndex(e => e.ProductId == productId);
        if (index >= 0)
            entries[index] = (productId, quantity);
        else
            entries.Add((productId, quantity));
        WriteEntries(entries);
    }

    public void RemoveLine(int userId, int productId)
    {
        var entries = ReadEntries();
        entries.RemoveAll(e => e.ProductId == productId);
        WriteEntries(entries);
    }

    public void Clear(int userId) => session.Remove(SessionKey);

    private List<(int ProductId, int Quantity)> ReadEntries()
    {
        var json = session.GetString(SessionKey);
        if (string.IsNullOrEmpty(json)) return [];
        var pairs = JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? [];
        return pairs.Select(p => (p.Key, p.Value)).ToList();
    }

    private void WriteEntries(List<(int ProductId, int Quantity)> entries) =>
        session.SetString(SessionKey, JsonSerializer.Serialize(entries.ToDictionary(e => e.ProductId, e => e.Quantity)));
}
