using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

public class DemoCatalogTests
{
    private readonly DemoCatalog _catalog = new();

    [Fact]
    public void FindAnimal_ReturnsMatchingAnimal()
    {
        var animal = _catalog.FindAnimal(1);

        Assert.NotNull(animal);
        Assert.Equal(1, animal!.ProductId);
    }

    [Fact]
    public void FindAnimal_ReturnsNullForUnknownId()
    {
        Assert.Null(_catalog.FindAnimal(9999));
    }

    [Fact]
    public void AvailableAnimals_OnlyReturnsAnimalsWithAvailableStatus()
    {
        var available = _catalog.AvailableAnimals().ToList();

        Assert.NotEmpty(available);
        Assert.All(available, a => Assert.Equal(AnimalStatus.Available, a.Status));
    }

    [Fact]
    public void AnimalGroups_EachAnimalAppearsExactlyOnce()
    {
        var groups = _catalog.AnimalGroups().ToList();
        var seenIds = groups.SelectMany(g => g.Select(a => a.ProductId)).ToList();

        Assert.Equal(_catalog.Animals.Count, seenIds.Count);
        Assert.Equal(seenIds.Distinct().Count(), seenIds.Count);
    }

    [Fact]
    public void AnimalGroups_BondedAnimalsAreGroupedTogether()
    {
        var groups = _catalog.AnimalGroups().ToList();

        var pelleGroup = groups.Single(g => g.Any(a => a.Name == "Pelle"));

        Assert.Equal(2, pelleGroup.Count);
        Assert.Contains(pelleGroup, a => a.Name == "Basse");
    }

    [Fact]
    public void AnimalGroups_UnbondedAnimalIsItsOwnGroup()
    {
        var groups = _catalog.AnimalGroups().ToList();

        var stormGroup = groups.Single(g => g.Any(a => a.Name == "Storm"));

        Assert.Single(stormGroup);
    }
}
