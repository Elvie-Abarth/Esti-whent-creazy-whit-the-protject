using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages;

namespace MarsvinWebExample.Tests.Pages;

public class IndexModelTests
{
    [Fact]
    public void OnGet_PopulatesGroupsAvailableCountAndEssentials()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet();

        Assert.NotEmpty(model.Groups);
        Assert.True(model.Groups.Count <= 2);
        Assert.Equal(catalog.AvailableAnimals().Count(), model.AvailableCount);

        Assert.True(model.Essentials.Count <= 3);
        Assert.All(model.Essentials, item =>
            Assert.True(item.Category is AccessoryCategory.Hay or AccessoryCategory.Food or AccessoryCategory.Cage));
    }
}
