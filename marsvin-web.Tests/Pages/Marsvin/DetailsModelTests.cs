using MarsvinWebExample.Data;
using MarsvinWebExample.Pages.Marsvin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Tests.Pages.Marsvin;

public class DetailsModelTests
{
    [Fact]
    public void OnGet_KnownId_SetsAnimalAndReturnsPage()
    {
        var catalog = new DemoCatalog();
        var model = new DetailsModel(catalog);

        var result = model.OnGet(1);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Pelle", model.Animal.Name);
    }

    [Fact]
    public void OnGet_BondedAnimal_SetsPartner()
    {
        var catalog = new DemoCatalog();
        var model = new DetailsModel(catalog);

        model.OnGet(1);

        Assert.NotNull(model.Partner);
        Assert.Equal("Basse", model.Partner!.Name);
    }

    [Fact]
    public void OnGet_UnbondedAnimal_HasNoPartner()
    {
        var catalog = new DemoCatalog();
        var model = new DetailsModel(catalog);

        model.OnGet(6);

        Assert.Null(model.Partner);
    }

    [Fact]
    public void OnGet_UnknownId_ReturnsNotFound()
    {
        var catalog = new DemoCatalog();
        var model = new DetailsModel(catalog);

        var result = model.OnGet(9999);

        Assert.IsType<NotFoundResult>(result);
    }
}
