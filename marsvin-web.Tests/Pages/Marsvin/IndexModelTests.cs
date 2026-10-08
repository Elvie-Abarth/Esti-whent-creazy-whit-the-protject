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

    [Fact]
    public void OnGet_ListsGuineaPigsWithAPhotoBeforeThoseWithOnlyADrawing()
    {
        var model = new IndexModel(new DemoCatalog());

        model.OnGet();

        var hasPhoto = model.Groups.Select(g => g.Any(a => !string.IsNullOrWhiteSpace(a.PhotoUrl))).ToList();
        Assert.Contains(true, hasPhoto);
        Assert.Contains(false, hasPhoto);
        // Once the first group without a photo appears, none with a photo may follow.
        Assert.DoesNotContain(true, hasPhoto.SkipWhile(photo => photo));
    }
}
