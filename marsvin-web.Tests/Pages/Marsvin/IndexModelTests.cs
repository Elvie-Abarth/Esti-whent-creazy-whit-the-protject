using MarsvinWebExample.Data;
using MarsvinWebExample.Pages.Marsvin;

namespace MarsvinWebExample.Tests.Pages.Marsvin;

public class IndexModelTests
{
    [Fact]
    public void OnGet_PopulatesAllAnimalGroups()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet();

        var totalAnimals = model.Groups.Sum(g => g.Count);
        Assert.Equal(catalog.Animals.Count, totalAnimals);
    }
}
