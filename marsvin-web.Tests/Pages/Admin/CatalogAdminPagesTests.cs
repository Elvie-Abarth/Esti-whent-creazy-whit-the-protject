using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AnimalPages = MarsvinWebExample.Pages.Admin.Animals;
using AuditLogPages = MarsvinWebExample.Pages.Admin.AuditLog;
using ProductPages = MarsvinWebExample.Pages.Admin.Products;
using PromotionPages = MarsvinWebExample.Pages.Admin.Promotions;
using StockPages = MarsvinWebExample.Pages.Admin.Stock;

namespace MarsvinWebExample.Tests.Pages.Admin;

/// <summary>
/// The admin pages that change the catalog - stock, accessories, guinea pigs
/// and promotions - called directly: what each handler saves, what it
/// refuses, and that every change leaves an audit-log entry naming who did
/// it. Who is allowed to reach these pages at all is a pipeline matter, and
/// is covered over real HTTP in EndToEndAdminAccessTests. Every test makes
/// its own throwaway row and removes it again, as SqlCatalogAdminTests does.
/// </summary>
[Collection("SqlCatalog collection")]
public class CatalogAdminPagesTests(SqlCatalogFixture fixture)
{
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private readonly SqlPromotionStore _promotions = new(fixture.ConnectionString);
    private readonly SqlAuditLogStore _audit = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);

    private ApplicationUser NewAdmin([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, UserRole.Admin);
        return _users.FindByEmail(email)!;
    }

    private static PageContext SignedInAs(ApplicationUser user) => TestAuth.ContextFor(user.UserId, user.Role.ToString());

    private static string Unique(string label) => $"{label}-{Guid.NewGuid():N}";

    private StockProduct NewProductInCatalog(int stock = 10)
    {
        var name = Unique("Testvare");
        _catalog.CreateStockProduct(new StockProduct
        {
            ProductId = 0, Name = name, Description = "Test description", Sku = Unique("SKU")[..20],
            Category = AccessoryCategory.Toy, StockQuantity = stock, Price = 49
        });
        return _catalog.Accessories.Single(p => p.Name == name);
    }

    private Animal NewAnimalInCatalog()
    {
        var name = Unique("Testdyr");
        _catalog.CreateAnimal(new Animal
        {
            ProductId = 0, Name = name, Description = "Test description", Breed = "Test breed", Sex = Sex.Boar,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-70)), Colour = "Test colour",
            CoatPrimary = "#000000", CoatSecondary = "#FFFFFF", Status = AnimalStatus.Available, Price = 350
        });
        return _catalog.Animals.Single(a => a.Name == name);
    }

    private AuditLogEntry? AuditEntry(string action, string detailsContain) =>
        _audit.GetRecent(200).FirstOrDefault(e => e.Action == action && e.Details.Contains(detailsContain));

    // ---- Stock (Admin + Employee) ----

    [Fact]
    public void Stock_UpdateStock_SavesTheNewCount_AndAuditsOldAndNew()
    {
        var admin = NewAdmin();
        var product = NewProductInCatalog(stock: 10);
        try
        {
            var model = new StockPages.IndexModel(_catalog, _catalog, _audit) { PageContext = SignedInAs(admin) };

            var result = model.OnPostUpdateStock(product.ProductId, 3);

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal(3, _catalog.Accessories.Single(p => p.ProductId == product.ProductId).StockQuantity);
            var entry = AuditEntry("Product.StockChanged", product.Name);
            Assert.NotNull(entry);
            Assert.Equal(admin.UserId, entry!.ActorUserId);
            Assert.Contains("10 -> 3", entry.Details);
        }
        finally
        {
            _catalog.DeleteStockProduct(product.ProductId);
        }
    }

    [Fact]
    public void Stock_UpdateStock_WithANegativeNumber_ChangesNothingAndAuditsNothing()
    {
        var admin = NewAdmin();
        var product = NewProductInCatalog(stock: 10);
        try
        {
            var model = new StockPages.IndexModel(_catalog, _catalog, _audit) { PageContext = SignedInAs(admin) };

            model.OnPostUpdateStock(product.ProductId, -5);

            Assert.Equal(10, _catalog.Accessories.Single(p => p.ProductId == product.ProductId).StockQuantity);
            Assert.Null(AuditEntry("Product.StockChanged", product.Name));
        }
        finally
        {
            _catalog.DeleteStockProduct(product.ProductId);
        }
    }

    [Fact]
    public void Stock_UpdateStatus_ChangesTheAnimalsStatus_AndAuditsIt()
    {
        var admin = NewAdmin();
        var animal = NewAnimalInCatalog();
        try
        {
            var model = new StockPages.IndexModel(_catalog, _catalog, _audit) { PageContext = SignedInAs(admin) };

            model.OnPostUpdateStatus(animal.ProductId, AnimalStatus.Reserved);

            Assert.Equal(AnimalStatus.Reserved, _catalog.FindAnimal(animal.ProductId)!.Status);
            var entry = AuditEntry("Animal.StatusChanged", animal.Name);
            Assert.NotNull(entry);
            Assert.Contains("Available -> Reserved", entry!.Details);
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void Stock_OnGet_ListsAccessoriesAndAnimals()
    {
        var model = new StockPages.IndexModel(_catalog, _catalog, _audit);

        model.OnGet();

        Assert.NotEmpty(model.Accessories);
        Assert.NotEmpty(model.Animals);
    }

    // ---- Accessories (Admin) ----

    private static ProductPages.EditModel.InputModel ValidProductInput(string name) => new()
    {
        Name = name, Description = "Test description", Sku = Unique("SKU")[..20],
        Category = AccessoryCategory.Toy, Price = 79, StockQuantity = 4, WeightGrams = 250, Brand = "  "
    };

    [Fact]
    public async Task Products_Edit_Post_WithIdZero_CreatesTheProduct_AndAuditsIt()
    {
        var admin = NewAdmin();
        var name = Unique("Ny vare");
        var model = new ProductPages.EditModel(_catalog, _catalog, _audit, environment: null!)
        {
            PageContext = SignedInAs(admin), Input = ValidProductInput(name)
        };

        var result = await model.OnPostAsync(0);

        var created = _catalog.Accessories.Single(p => p.Name == name);
        try
        {
            Assert.Equal("Index", Assert.IsType<RedirectToPageResult>(result).PageName);
            Assert.Equal(79, created.Price);
            Assert.Equal(250, created.WeightGrams);
            Assert.Null(created.Brand);     // a brand of only spaces is stored as "no brand"
            Assert.Equal(admin.UserId, AuditEntry("Product.Created", name)!.ActorUserId);
        }
        finally
        {
            _catalog.DeleteStockProduct(created.ProductId);
        }
    }

    [Fact]
    public async Task Products_Edit_Post_WithAnExistingId_UpdatesIt_AndAuditsIt()
    {
        var admin = NewAdmin();
        var product = NewProductInCatalog();
        try
        {
            var input = ValidProductInput(product.Name);
            input.Price = 123;
            var model = new ProductPages.EditModel(_catalog, _catalog, _audit, environment: null!)
            {
                PageContext = SignedInAs(admin), Input = input
            };

            await model.OnPostAsync(product.ProductId);

            Assert.Equal(123, _catalog.Accessories.Single(p => p.ProductId == product.ProductId).Price);
            Assert.NotNull(AuditEntry("Product.Updated", product.Name));
        }
        finally
        {
            _catalog.DeleteStockProduct(product.ProductId);
        }
    }

    [Fact]
    public async Task Products_Edit_Post_WithInvalidInput_ShowsTheFormAgain_AndSavesNothing()
    {
        var admin = NewAdmin();
        var name = Unique("Ugyldig vare");
        var model = new ProductPages.EditModel(_catalog, _catalog, _audit, environment: null!)
        {
            PageContext = SignedInAs(admin), Input = ValidProductInput(name)
        };
        // What model binding would have recorded for a price outside 0-100,000.
        model.ModelState.AddModelError("Input.Price", "out of range");

        var result = await model.OnPostAsync(0);

        Assert.IsType<PageResult>(result);
        Assert.DoesNotContain(_catalog.Accessories, p => p.Name == name);
        Assert.Null(AuditEntry("Product.Created", name));
    }

    [Fact]
    public void Products_Edit_Get_FillsTheForm_ForAnExistingProduct_AndIs404ForAnUnknownOne()
    {
        var product = NewProductInCatalog();
        try
        {
            var model = new ProductPages.EditModel(_catalog, _catalog, _audit, environment: null!);

            Assert.IsType<PageResult>(model.OnGet(product.ProductId));
            Assert.Equal(product.Name, model.Input.Name);
            Assert.False(model.IsNew);

            Assert.IsType<NotFoundResult>(model.OnGet(int.MaxValue));

            Assert.IsType<PageResult>(model.OnGet(0));
            Assert.True(model.IsNew);
        }
        finally
        {
            _catalog.DeleteStockProduct(product.ProductId);
        }
    }

    [Fact]
    public void Products_Index_Delete_RemovesTheProduct_AndAuditsItByName()
    {
        var admin = NewAdmin();
        var product = NewProductInCatalog();
        var model = new ProductPages.IndexModel(_catalog, _catalog, _audit) { PageContext = SignedInAs(admin) };

        model.OnPostDelete(product.ProductId);

        Assert.DoesNotContain(_catalog.Accessories, p => p.ProductId == product.ProductId);
        Assert.Equal(admin.UserId, AuditEntry("Product.Deleted", product.Name)!.ActorUserId);
    }

    // ---- Guinea pigs (Admin) ----

    private static AnimalPages.EditModel.InputModel ValidAnimalInput(string name) => new()
    {
        Name = name, Description = "Test description", Breed = "Test breed", Sex = Sex.Sow,
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), Colour = "Test colour",
        Status = AnimalStatus.Available, Price = 400
    };

    [Fact]
    public async Task Animals_Edit_Post_WithIdZero_CreatesTheAnimal_AndAuditsIt()
    {
        var admin = NewAdmin();
        var name = Unique("Nyt dyr");
        var model = new AnimalPages.EditModel(_catalog, _catalog, _audit, environment: null!)
        {
            PageContext = SignedInAs(admin), Input = ValidAnimalInput(name)
        };

        var result = await model.OnPostAsync(0);

        var created = _catalog.Animals.Single(a => a.Name == name);
        try
        {
            Assert.Equal("Index", Assert.IsType<RedirectToPageResult>(result).PageName);
            Assert.Equal(Sex.Sow, created.Sex);
            Assert.Equal(400, created.Price);
            Assert.Equal(admin.UserId, AuditEntry("Animal.Created", name)!.ActorUserId);
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public async Task Animals_Edit_Post_WithAnExistingId_UpdatesIt_AndAuditsIt()
    {
        var admin = NewAdmin();
        var animal = NewAnimalInCatalog();
        try
        {
            var input = ValidAnimalInput(animal.Name);
            input.Status = AnimalStatus.Sold;
            var model = new AnimalPages.EditModel(_catalog, _catalog, _audit, environment: null!)
            {
                PageContext = SignedInAs(admin), Input = input
            };

            await model.OnPostAsync(animal.ProductId);

            Assert.Equal(AnimalStatus.Sold, _catalog.FindAnimal(animal.ProductId)!.Status);
            Assert.NotNull(AuditEntry("Animal.Updated", animal.Name));
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public async Task Animals_Edit_Post_WithInvalidInput_ShowsTheFormAgain_AndSavesNothing()
    {
        var admin = NewAdmin();
        var name = Unique("Ugyldigt dyr");
        var model = new AnimalPages.EditModel(_catalog, _catalog, _audit, environment: null!)
        {
            PageContext = SignedInAs(admin), Input = ValidAnimalInput(name)
        };
        model.ModelState.AddModelError("Input.CoatPrimary", "not a hex colour");

        var result = await model.OnPostAsync(0);

        Assert.IsType<PageResult>(result);
        Assert.DoesNotContain(_catalog.Animals, a => a.Name == name);
    }

    [Fact]
    public void Animals_Edit_Get_FillsTheForm_LeavesTheAnimalOutOfItsOwnPartnerList_AndIs404ForAnUnknownOne()
    {
        var animal = NewAnimalInCatalog();
        try
        {
            var model = new AnimalPages.EditModel(_catalog, _catalog, _audit, environment: null!);

            Assert.IsType<PageResult>(model.OnGet(animal.ProductId));
            Assert.Equal(animal.Name, model.Input.Name);
            Assert.DoesNotContain(model.OtherAnimals, a => a.ProductId == animal.ProductId);

            Assert.IsType<NotFoundResult>(model.OnGet(int.MaxValue));
        }
        finally
        {
            _catalog.DeleteAnimal(animal.ProductId);
        }
    }

    [Fact]
    public void Animals_Index_Delete_RemovesTheAnimal_AndAuditsItByName()
    {
        var admin = NewAdmin();
        var animal = NewAnimalInCatalog();
        var model = new AnimalPages.IndexModel(_catalog, _catalog, _audit) { PageContext = SignedInAs(admin) };

        model.OnPostDelete(animal.ProductId);

        Assert.Null(_catalog.FindAnimal(animal.ProductId));
        Assert.Equal(admin.UserId, AuditEntry("Animal.Deleted", animal.Name)!.ActorUserId);
    }

    // ---- Promotions (Admin) ----

    private static PromotionPages.EditModel.InputModel ValidPromotionInput(string title) => new()
    {
        Title = title, Description = "Test description", DiscountPercent = 15,
        StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
    };

    [Fact]
    public void Promotions_Edit_Post_CreatesThenUpdates_AndAuditsBoth()
    {
        var admin = NewAdmin();
        var title = Unique("Kampagne");
        var create = new PromotionPages.EditModel(_promotions, _catalog, _audit)
        {
            PageContext = SignedInAs(admin), Input = ValidPromotionInput(title)
        };

        var result = create.OnPost(0);

        var created = _promotions.GetAll().Single(p => p.Title == title);
        try
        {
            Assert.Equal("Index", Assert.IsType<RedirectToPageResult>(result).PageName);
            Assert.Equal(15, created.DiscountPercent);
            Assert.NotNull(AuditEntry("Promotion.Created", title));

            var input = ValidPromotionInput(title);
            input.DiscountPercent = 30;
            var update = new PromotionPages.EditModel(_promotions, _catalog, _audit)
            {
                PageContext = SignedInAs(admin), Input = input
            };

            update.OnPost(created.PromotionId);

            Assert.Equal(30, _promotions.FindById(created.PromotionId)!.DiscountPercent);
            Assert.NotNull(AuditEntry("Promotion.Updated", title));
        }
        finally
        {
            _promotions.Delete(created.PromotionId);
        }
    }

    [Fact]
    public void Promotions_Edit_Post_WithAnEndDateBeforeTheStart_IsRefused_AndSavesNothing()
    {
        var admin = NewAdmin();
        var title = Unique("Baglæns kampagne");
        var input = ValidPromotionInput(title);
        input.EndDate = input.StartDate.AddDays(-1);
        var model = new PromotionPages.EditModel(_promotions, _catalog, _audit)
        {
            PageContext = SignedInAs(admin), Input = input
        };

        var result = model.OnPost(0);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.DoesNotContain(_promotions.GetAll(), p => p.Title == title);
    }

    [Fact]
    public void Promotions_Edit_Get_StartsANewOneTodayForTwoWeeks_AndIs404ForAnUnknownOne()
    {
        var model = new PromotionPages.EditModel(_promotions, _catalog, _audit);

        Assert.IsType<PageResult>(model.OnGet(0));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), model.Input.StartDate);
        Assert.Equal(model.Input.StartDate.AddDays(14), model.Input.EndDate);
        Assert.NotEmpty(model.Products);

        Assert.IsType<NotFoundResult>(model.OnGet(int.MaxValue));
    }

    [Fact]
    public void Promotions_Index_Delete_RemovesThePromotion_AndAuditsItByTitle()
    {
        var admin = NewAdmin();
        var title = Unique("Slettes");
        _promotions.Create(new Promotion
        {
            Title = title, Description = "Test description", DiscountPercent = 10,
            StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        });
        var promotion = _promotions.GetAll().Single(p => p.Title == title);
        var model = new PromotionPages.IndexModel(_promotions, _audit) { PageContext = SignedInAs(admin) };

        model.OnPostDelete(promotion.PromotionId);

        Assert.Null(_promotions.FindById(promotion.PromotionId));
        Assert.Equal(admin.UserId, AuditEntry("Promotion.Deleted", title)!.ActorUserId);
    }

    // ---- Audit log (Admin, read-only) ----

    [Fact]
    public void AuditLog_OnGet_ShowsTheNewestEntryFirst()
    {
        var admin = NewAdmin();
        var marker = Unique("audit-marker");
        _audit.Record(admin.UserId, admin.DisplayName, "Test.Action", marker);
        var model = new AuditLogPages.IndexModel(_audit);

        model.OnGet();

        Assert.Equal(marker, model.Entries[0].Details);
        Assert.Equal(admin.DisplayName, model.Entries[0].ActorName);
        Assert.True(model.Entries.Count <= 200);
    }

    [Fact]
    public void AuditLog_PageModel_HasNoHandlerThatCouldChangeAnEntry()
    {
        // "Read-only after writing" is a property of the page itself: the only
        // handler it has is OnGet, and the store has no update or delete.
        var handlers = typeof(AuditLogPages.IndexModel).GetMethods()
            .Where(m => m.DeclaringType == typeof(AuditLogPages.IndexModel) && m.Name.StartsWith("On"))
            .Select(m => m.Name);
        Assert.Equal(["OnGet"], handlers);

        var storeMethods = typeof(IAuditLogStore).GetMethods().Select(m => m.Name).Order();
        Assert.Equal(["GetRecent", "Record"], storeMethods);
    }
}
