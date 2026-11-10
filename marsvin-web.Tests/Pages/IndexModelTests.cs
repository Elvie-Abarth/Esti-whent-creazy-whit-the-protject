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

    [Fact]
    public void OnGet_LeadsWithGuineaPigsThatHaveAPhoto()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet();

        // The front page should feature guinea pigs with a real photo, not the
        // drawing fallback - every group shown has at least one photographed pig.
        Assert.All(model.Groups, group =>
            Assert.Contains(group, animal => !string.IsNullOrWhiteSpace(animal.PhotoUrl)));
    }
}
