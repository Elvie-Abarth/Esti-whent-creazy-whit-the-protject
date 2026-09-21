using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Cart;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages.Cart;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private int NewCustomerId([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Customer);
        return _users.FindByEmail(email)!.UserId;
    }

    private IndexModel MakeModel(int userId) => new(_cart, _catalog)
    {
        PageContext = TestAuth.ContextFor(userId, "Customer")
    };

    private Animal NewUnbondedAvailableAnimal([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var animal = new Animal
        {
            ProductId = 0,
            Name = $"Solo-{caller}-{Guid.NewGuid():N}",
            Description = "test",
            Breed = "test",
            Sex = Sex.Boar,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-70)), // well past 4 weeks
            Colour = "test",
            CoatPrimary = "#000000",
            CoatSecondary = "#ffffff",
            Status = AnimalStatus.Available
        };
        _catalog.CreateAnimal(animal);
        return _catalog.Animals.Single(a => a.Name == animal.Name);
    }

    [Fact]
    public void OnPostAdd_AvailableAccessory_AddsLineToCart()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 104, quantity: 2);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(104, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void OnPostAdd_MoreThanAvailableStock_DoesNotAddAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        var stock = _catalog.Accessories.Single(p => p.ProductId == 109).StockQuantity;

        model.OnPostAdd(productId: 109, quantity: stock + 1);

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostAdd_CombinedWithWhatsAlreadyInTheCart_ExceedsStock_DoesNotAddAndSetsError()
    {
        // Regression test: OnPostAdd used to validate only the newly-added
        // quantity against stock, not the quantity already in the cart plus
        // the new add - so adding 5 more of something with 6 in stock when 5
        // are already in the cart passed (5 <= 6) and left 10 in the cart.
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        var stock = _catalog.Accessories.Single(p => p.ProductId == 109).StockQuantity;
        model.OnPostAdd(productId: 109, quantity: stock); // fills the cart to exactly the stock limit

        model.OnPostAdd(productId: 109, quantity: 1); // one more should now be rejected

        Assert.Equal(stock, Assert.Single(_cart.GetLines(userId)).Quantity);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostAdd_UnavailableAnimal_DoesNotAddAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 5, quantity: 1); // Freja - seeded Reserved

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostAdd_BondedAnimal_AddsWithQuantityOneAndNeedsNoConfirmation()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        // Pelle - seeded Available, bonded to Basse - already comes with a
        // partner, so no confirmNotAlone/companionNote is needed here.
        model.OnPostAdd(productId: 1, quantity: 1);

        var line = _cart.GetLines(userId).Single(l => l.ProductId == 1);
        Assert.True(line.IsAnimal);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public void OnPostAdd_BondedAnimal_AlsoAddsItsPartnerToTheCart()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        // Marsvin/Details promises "the two move in together - combined price"
        // for a bonded pair, so adding Pelle (1) should bring Basse (2) along.
        model.OnPostAdd(productId: 1, quantity: 1);

        var lines = _cart.GetLines(userId);
        Assert.Contains(lines, l => l.ProductId == 1);
        Assert.Contains(lines, l => l.ProductId == 2);
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void OnPostAdd_BondedAnimal_WhenPartnerAlreadyInCart_DoesNotDuplicateIt()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 2, quantity: 1); // Basse first
        model.OnPostAdd(productId: 1, quantity: 1); // then Pelle

        var basseLine = _cart.GetLines(userId).Single(l => l.ProductId == 2);
        Assert.Equal(1, basseLine.Quantity);
    }

    [Fact]
    public void OnPostAdd_UnbondedAnimalWithoutConfirmation_DoesNotAddAndSetsError()
    {
        var userId = NewCustomerId();
        var animal = NewUnbondedAvailableAnimal();
        var model = MakeModel(userId);
        try
        {
            model.OnPostAdd(productId: animal.ProductId, quantity: 1);

            Assert.Empty(_cart.GetLines(userId));
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void OnPostAdd_UnbondedAnimalConfirmedButNoCompanionNote_DoesNotAdd()
    {
        var userId = NewCustomerId();
        var animal = NewUnbondedAvailableAnimal();
        var model = MakeModel(userId);
        try
        {
            // A checked box with no actual description of the companion/herd
            // isn't enough - companionNote is required too.
            model.OnPostAdd(productId: animal.ProductId, quantity: 1, confirmNotAlone: true, companionNote: "   ");

            Assert.Empty(_cart.GetLines(userId));
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void OnPostAdd_UnbondedAnimalConfirmedWithCompanionNote_Adds()
    {
        var userId = NewCustomerId();
        var animal = NewUnbondedAvailableAnimal();
        var model = MakeModel(userId);
        try
        {
            model.OnPostAdd(productId: animal.ProductId, quantity: 1,
                confirmNotAlone: true, companionNote: "Mit marsvin Nisse");

            var line = Assert.Single(_cart.GetLines(userId));
            Assert.True(line.IsAnimal);
        }
        finally
        {
            _cart.RemoveLine(userId, animal.ProductId);
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void OnPostAdd_Success_SetsToastMessageWithTheProductName()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 104, quantity: 1);

        Assert.Contains("mælkebøtte", model.ToastMessage);
    }

    [Fact]
    public void OnPostAdd_WithLocalReturnUrl_RedirectsThereInsteadOfTheCartPage()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        var result = model.OnPostAdd(productId: 104, quantity: 1, returnUrl: "/Tilbehor?kategori=Hay#product-104");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Tilbehor?kategori=Hay#product-104", redirect.Url);
    }

    [Fact]
    public void OnPostAdd_WithoutReturnUrl_RedirectsToTheCartPage()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        var result = model.OnPostAdd(productId: 104, quantity: 1);

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public void OnPostAdd_WithOffSiteReturnUrl_IgnoresItAndRedirectsToTheCartPageInstead()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        // Url.IsLocalUrl must reject this - a returnUrl is attacker-controlled
        // (it's a plain form field), so honouring an absolute URL here would be
        // an open redirect off the site straight from the add-to-cart handler.
        var result = model.OnPostAdd(productId: 104, quantity: 1, returnUrl: "https://evil.example.com/phish");

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public void OnPostAdd_UnknownProductId_SetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);

        model.OnPostAdd(productId: 999999, quantity: 1);

        Assert.Empty(_cart.GetLines(userId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostUpdateQuantity_WithinStock_UpdatesTheLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 1);

        model.OnPostUpdateQuantity(productId: 104, quantity: 3);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public void OnPostUpdateQuantity_MoreThanAvailableStock_LeavesQuantityUnchangedAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 109, quantity: 1);
        var stock = _catalog.Accessories.Single(p => p.ProductId == 109).StockQuantity;

        model.OnPostUpdateQuantity(productId: 109, quantity: stock + 1);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(1, line.Quantity);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostUpdateQuantity_ZeroOrLess_RemovesTheLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 2);

        model.OnPostUpdateQuantity(productId: 104, quantity: 0);

        Assert.Empty(_cart.GetLines(userId));
    }

    [Fact]
    public void OnPostUpdateQuantity_Animal_LeavesQuantityAtOneAndSetsError()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 1, quantity: 1); // Pelle - seeded Available

        model.OnPostUpdateQuantity(productId: 1, quantity: 2);

        var line = _cart.GetLines(userId).Single(l => l.ProductId == 1);
        Assert.Equal(1, line.Quantity);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostRemove_RemovesOnlyThatLine()
    {
        var userId = NewCustomerId();
        var model = MakeModel(userId);
        model.OnPostAdd(productId: 104, quantity: 1);
        model.OnPostAdd(productId: 109, quantity: 1);

        model.OnPostRemove(productId: 104);

        var line = Assert.Single(_cart.GetLines(userId));
        Assert.Equal(109, line.ProductId);
    }
}
