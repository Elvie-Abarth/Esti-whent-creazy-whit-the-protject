using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Tilbehor;

namespace MarsvinWebExample.Tests.Pages.Tilbehor;

public class IndexModelTests
{
    [Fact]
    public void OnGet_NoCategory_ReturnsAllAccessories()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet(null);

        Assert.Null(model.Active);
        Assert.Equal(catalog.Accessories.Count, model.Items.Count);
    }

    [Fact]
    public void OnGet_ValidCategory_FiltersItems()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet("hay");

        Assert.Equal(AccessoryCategory.Hay, model.Active);
        Assert.NotEmpty(model.Items);
        Assert.All(model.Items, item => Assert.Equal(AccessoryCategory.Hay, item.Category));
    }

    [Fact]
    public void OnGet_InvalidCategory_FallsBackToAllItems()
    {
        var catalog = new DemoCatalog();
        var model = new IndexModel(catalog);

        model.OnGet("not-a-real-category");

        Assert.Null(model.Active);
        Assert.Equal(catalog.Accessories.Count, model.Items.Count);
    }

    [Fact]
    public void Categories_CoversEveryAccessoryCategoryInUse()
    {
        var catalog = new DemoCatalog();
        var categoriesInData = catalog.Accessories.Select(a => a.Category).Distinct();

        foreach (var category in categoriesInData)
        {
            Assert.Contains(IndexModel.Categories, c => c.Value == category);
        }
    }
}
