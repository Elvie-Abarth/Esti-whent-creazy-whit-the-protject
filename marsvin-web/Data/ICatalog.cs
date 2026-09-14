using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// What a page needs from the catalog, regardless of where the data lives.
/// <see cref="DemoCatalog"/> serves it from an in-memory list; <see cref="SqlCatalog"/>
/// serves it from SQL Server LocalDB.
/// </summary>
public interface ICatalog
{
    IReadOnlyList<Animal> Animals { get; }
    IReadOnlyList<StockProduct> Accessories { get; }

    Animal? FindAnimal(int id);

    /// <summary>
    /// Either kind of product by id, whichever it turns out to be, or null.
    /// Resolves a single row directly rather than loading the whole Animals
    /// or Accessories table to pick one id back out of it - see Cart/Index
    /// and Profile.OnPostReorder, which each resolve a handful of ids per
    /// request and previously did exactly that.
    /// </summary>
    Product? FindProduct(int id);

    IEnumerable<Animal> AvailableAnimals();

    /// <summary>Groups bonded animals together so they are always shown as a pair.</summary>
    IEnumerable<IReadOnlyList<Animal>> AnimalGroups();
}
