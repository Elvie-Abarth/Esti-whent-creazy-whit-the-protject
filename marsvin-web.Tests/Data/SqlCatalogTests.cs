using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlCatalogTests
{
    private readonly SqlCatalog _catalog;
    private readonly DemoCatalog _demo = new();

    public SqlCatalogTests(SqlCatalogFixture fixture)
    {
        _catalog = new SqlCatalog(fixture.ConnectionString);
    }

    [Fact]
    public void Animals_RowCountMatchesDemoCatalog()
    {
        Assert.Equal(_demo.Animals.Count, _catalog.Animals.Count);
    }

    [Fact]
    public void Accessories_RowCountMatchesDemoCatalog()
    {
        Assert.Equal(_demo.Accessories.Count, _catalog.Accessories.Count);
    }

    [Fact]
    public void Animal_FieldsRoundTripThroughSql()
    {
        var pelle = _catalog.FindAnimal(1);

        Assert.NotNull(pelle);
        Assert.Equal("Pelle", pelle!.Name);
        Assert.Equal("Abyssinier", pelle.Breed);
        Assert.Equal("Abyssinian", pelle.BreedEn);
        Assert.Equal(Sex.Boar, pelle.Sex);
        Assert.Equal(AnimalStatus.Available, pelle.Status);
        Assert.Equal(2, pelle.BondedWithId);
        Assert.Equal(450m, pelle.Price);
    }

    [Fact]
    public void Animal_NullableEnglishFieldsSurviveAsNullWhereAbsent()
    {
        // PhotoUrl is never set in DemoCatalog - confirms DBNull round-trips as null, not "".
        var pelle = _catalog.FindAnimal(1);

        Assert.Null(pelle!.PhotoUrl);
    }

    [Fact]
    public void StockProduct_FieldsRoundTripThroughSql()
    {
        var hay = _catalog.Accessories.Single(a => a.Sku == "HAY-TIM-2");

        Assert.Equal("Timothy-hø, 2 kg", hay.Name);
        Assert.Equal("Timothy hay, 2 kg", hay.NameEn);
        Assert.Equal(AccessoryCategory.Hay, hay.Category);
        Assert.Equal(48, hay.StockQuantity);
        Assert.Equal("pose", hay.Unit);
    }

    [Fact]
    public void FindAnimal_ReturnsNullForUnknownId()
    {
        Assert.Null(_catalog.FindAnimal(9999));
    }

    [Fact]
    public void AvailableAnimals_ExcludesReservedAndNotForSale()
    {
        var available = _catalog.AvailableAnimals().ToList();

        Assert.All(available, a => Assert.Equal(AnimalStatus.Available, a.Status));
        Assert.DoesNotContain(available, a => a.Name == "Freja"); // Reserved
        Assert.DoesNotContain(available, a => a.Name == "Storm"); // NotForSale
    }

    [Fact]
    public void AnimalGroups_BondedAnimalsAreGroupedTogetherOverSql()
    {
        var groups = _catalog.AnimalGroups().ToList();

        var pelleGroup = groups.Single(g => g.Any(a => a.Name == "Pelle"));
        Assert.Equal(2, pelleGroup.Count);
        Assert.Contains(pelleGroup, a => a.Name == "Basse");

        var seenIds = groups.SelectMany(g => g.Select(a => a.ProductId)).ToList();
        Assert.Equal(seenIds.Distinct().Count(), seenIds.Count);
    }
}
