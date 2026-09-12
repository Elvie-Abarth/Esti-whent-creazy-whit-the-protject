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
    IEnumerable<Animal> AvailableAnimals();

    /// <summary>Groups bonded animals together so they are always shown as a pair.</summary>
    IEnumerable<IReadOnlyList<Animal>> AnimalGroups();
}
