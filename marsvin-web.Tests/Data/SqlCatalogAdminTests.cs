using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// The write side of SqlCatalog (ICatalogAdmin) - Create/Update/Delete for
/// both animals and stock products, plus the two employee-level quick
/// updates. SqlCatalogTests covers the read side against the seeded demo
/// data; every test here creates its own throwaway row (unique name/SKU per
/// test) and cleans it up in a finally block, the same discipline as the
/// throwaway-animal tests elsewhere in this shared fixture.
/// </summary>
[Collection("SqlCatalog collection")]
public class SqlCatalogAdminTests(SqlCatalogFixture fixture)
{
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);

    private static Animal NewAnimal(string name) => new()
    {
        ProductId = 0,
        Name = name,
        Description = "Test description",
        Breed = "Test breed",
        Sex = Sex.Boar,
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-70)),
        Colour = "Test colour",
        CoatPrimary = "#000000",
        CoatSecondary = "#FFFFFF",
        Status = AnimalStatus.Available,
        Price = 350
    };

    private static StockProduct NewStockProduct(string name, string sku) => new()
    {
        ProductId = 0,
        Name = name,
        Description = "Test description",
        Sku = sku,
        Category = AccessoryCategory.Toy,
        StockQuantity = 10,
        Price = 49
    };

    [Fact]
    public void CreateAnimal_ThenFindAnimal_RoundTripsFields()
    {
        var animal = NewAnimal($"{nameof(CreateAnimal_ThenFindAnimal_RoundTripsFields)}-{Guid.NewGuid():N}");
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);
        try
        {
            var found = _catalog.FindAnimal(created.ProductId);

            Assert.NotNull(found);
            Assert.Equal(animal.Description, found!.Description);
            Assert.Equal(animal.Breed, found.Breed);
            Assert.Equal(animal.Sex, found.Sex);
            Assert.Equal(animal.Status, found.Status);
            Assert.Equal(animal.Price, found.Price);
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void UpdateAnimal_ChangesThePersistedFields()
    {
        var animal = NewAnimal($"{nameof(UpdateAnimal_ChangesThePersistedFields)}-{Guid.NewGuid():N}");
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);
        try
        {
            // Animal is a plain class, not a record - no `with` expression
            // available, so the updated row is a fresh object copying the
            // unchanged fields from what CreateAnimal actually persisted.
            var updated = new Animal
            {
                ProductId = created.ProductId,
                Name = created.Name,
                Description = "Updated description",
                Breed = created.Breed,
                Sex = created.Sex,
                DateOfBirth = created.DateOfBirth,
                Colour = created.Colour,
                CoatPrimary = created.CoatPrimary,
                CoatSecondary = created.CoatSecondary,
                Status = created.Status,
                Price = 999
            };

            _catalog.UpdateAnimal(updated);

            var found = _catalog.FindAnimal(created.ProductId);
            Assert.Equal("Updated description", found!.Description);
            Assert.Equal(999, found.Price);
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void DeleteAnimal_RemovesIt()
    {
        var animal = NewAnimal($"{nameof(DeleteAnimal_RemovesIt)}-{Guid.NewGuid():N}");
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);

        _catalog.DeleteAnimal(created.ProductId);

        Assert.Null(_catalog.FindAnimal(created.ProductId));
    }

    [Fact]
    public void UpdateAnimalStatus_ChangesOnlyStatus()
    {
        var animal = NewAnimal($"{nameof(UpdateAnimalStatus_ChangesOnlyStatus)}-{Guid.NewGuid():N}");
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);
        try
        {
            _catalog.UpdateAnimalStatus(created.ProductId, AnimalStatus.Sold);

            var found = _catalog.FindAnimal(created.ProductId);
            Assert.Equal(AnimalStatus.Sold, found!.Status);
            Assert.Equal(animal.Description, found.Description); // untouched
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void CreateStockProduct_ThenRoundTripsFields()
    {
        var sku = $"TEST-{Guid.NewGuid():N}";
        var product = NewStockProduct($"{nameof(CreateStockProduct_ThenRoundTripsFields)}-{Guid.NewGuid():N}", sku);
        _catalog.CreateStockProduct(product);
        var created = _catalog.Accessories.Single(p => p.Sku == sku);
        try
        {
            Assert.Equal(product.Description, created.Description);
            Assert.Equal(product.Category, created.Category);
            Assert.Equal(product.StockQuantity, created.StockQuantity);
            Assert.Equal(product.Price, created.Price);
        }
        finally
        {
            _catalog.DeleteStockProduct(created.ProductId);
        }
    }

    [Fact]
    public void UpdateStockProduct_ChangesThePersistedFields()
    {
        var sku = $"TEST-{Guid.NewGuid():N}";
        var product = NewStockProduct($"{nameof(UpdateStockProduct_ChangesThePersistedFields)}-{Guid.NewGuid():N}", sku);
        _catalog.CreateStockProduct(product);
        var created = _catalog.Accessories.Single(p => p.Sku == sku);
        try
        {
            // StockProduct is a plain class too - same reasoning as UpdateAnimal above.
            var updated = new StockProduct
            {
                ProductId = created.ProductId,
                Name = created.Name,
                Description = "Updated description",
                Sku = created.Sku,
                Category = created.Category,
                StockQuantity = 42,
                Price = 199
            };

            _catalog.UpdateStockProduct(updated);

            var found = _catalog.Accessories.Single(p => p.ProductId == created.ProductId);
            Assert.Equal("Updated description", found.Description);
            Assert.Equal(199, found.Price);
            Assert.Equal(42, found.StockQuantity);
        }
        finally
        {
            _catalog.DeleteStockProduct(created.ProductId);
        }
    }

    [Fact]
    public void DeleteStockProduct_RemovesIt()
    {
        var sku = $"TEST-{Guid.NewGuid():N}";
        var product = NewStockProduct($"{nameof(DeleteStockProduct_RemovesIt)}-{Guid.NewGuid():N}", sku);
        _catalog.CreateStockProduct(product);
        var created = _catalog.Accessories.Single(p => p.Sku == sku);

        _catalog.DeleteStockProduct(created.ProductId);

        Assert.DoesNotContain(_catalog.Accessories, p => p.ProductId == created.ProductId);
    }

    [Fact]
    public void UpdateStockQuantity_ChangesOnlyTheQuantity()
    {
        var sku = $"TEST-{Guid.NewGuid():N}";
        var product = NewStockProduct($"{nameof(UpdateStockQuantity_ChangesOnlyTheQuantity)}-{Guid.NewGuid():N}", sku);
        _catalog.CreateStockProduct(product);
        var created = _catalog.Accessories.Single(p => p.Sku == sku);
        try
        {
            _catalog.UpdateStockQuantity(created.ProductId, 3);

            var found = _catalog.Accessories.Single(p => p.ProductId == created.ProductId);
            Assert.Equal(3, found.StockQuantity);
            Assert.Equal(product.Description, found.Description); // untouched
            Assert.Equal(product.Price, found.Price); // untouched
        }
        finally
        {
            _catalog.DeleteStockProduct(created.ProductId);
        }
    }
}
