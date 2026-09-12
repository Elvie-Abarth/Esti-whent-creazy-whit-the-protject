using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Write side of the catalog - creating, editing and removing animals and
/// stock products, plus the quick stock/status updates employees make.
/// Separate from <see cref="ICatalog"/> (the read side every page uses)
/// because only the admin/employee area needs it, and only SqlCatalog
/// implements it - DemoCatalog is a fixed in-memory fixture for tests.
/// </summary>
public interface ICatalogAdmin
{
    void CreateAnimal(Animal animal);
    void UpdateAnimal(Animal animal);
    void DeleteAnimal(int productId);

    void CreateStockProduct(StockProduct product);
    void UpdateStockProduct(StockProduct product);
    void DeleteStockProduct(int productId);

    /// <summary>Employee-level quick update: adjust stock count without touching the rest of the product.</summary>
    void UpdateStockQuantity(int productId, int quantity);

    /// <summary>Employee-level quick update: change an animal's status without touching the rest of its record.</summary>
    void UpdateAnimalStatus(int productId, AnimalStatus status);
}
