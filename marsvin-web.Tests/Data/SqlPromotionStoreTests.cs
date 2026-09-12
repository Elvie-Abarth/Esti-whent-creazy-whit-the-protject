using MarsvinWebExample.Data;
using MarsvinWebExample.Models;

namespace MarsvinWebExample.Tests.Data;

[Collection("SqlCatalog collection")]
public class SqlPromotionStoreTests(SqlCatalogFixture fixture)
{
    private readonly SqlPromotionStore _promotions = new(fixture.ConnectionString);

    private static Promotion MakePromotion(int? productId = null) => new()
    {
        Title = $"Test kampagne {Guid.NewGuid():N}",
        Description = "test",
        DiscountPercent = 15,
        ProductId = productId,
        StartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 1, 31),
        IsActive = true
    };

    [Fact]
    public void Create_ThenGetAll_IncludesTheNewPromotion()
    {
        var promotion = MakePromotion();

        _promotions.Create(promotion);

        Assert.Contains(_promotions.GetAll(), p => p.Title == promotion.Title);
    }

    [Fact]
    public void Create_WithProductId_RoundTripsTheLink()
    {
        var promotion = MakePromotion(productId: 101);

        _promotions.Create(promotion);

        var found = _promotions.GetAll().Single(p => p.Title == promotion.Title);
        Assert.Equal(101, found.ProductId);
    }

    [Fact]
    public void Create_WithoutProductId_StoresNull()
    {
        var promotion = MakePromotion(productId: null);

        _promotions.Create(promotion);

        var found = _promotions.GetAll().Single(p => p.Title == promotion.Title);
        Assert.Null(found.ProductId);
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        Assert.Null(_promotions.FindById(999999));
    }

    [Fact]
    public void Update_ChangesStoredFields()
    {
        var promotion = MakePromotion();
        _promotions.Create(promotion);
        var created = _promotions.GetAll().Single(p => p.Title == promotion.Title);

        var updated = new Promotion
        {
            PromotionId = created.PromotionId,
            Title = created.Title,
            Description = created.Description,
            DiscountPercent = 50,
            ProductId = created.ProductId,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            IsActive = false
        };
        _promotions.Update(updated);

        var found = _promotions.FindById(created.PromotionId)!;
        Assert.Equal(50, found.DiscountPercent);
        Assert.False(found.IsActive);
    }

    [Fact]
    public void Delete_RemovesThePromotion()
    {
        var promotion = MakePromotion();
        _promotions.Create(promotion);
        var created = _promotions.GetAll().Single(p => p.Title == promotion.Title);

        _promotions.Delete(created.PromotionId);

        Assert.Null(_promotions.FindById(created.PromotionId));
    }

    [Fact]
    public void IsCurrentlyRunning_OutsideDateRange_IsFalse()
    {
        var promotion = new Promotion
        {
            Title = "Old promotion",
            Description = "test",
            DiscountPercent = 10,
            StartDate = new DateOnly(2020, 1, 1),
            EndDate = new DateOnly(2020, 1, 31),
            IsActive = true
        };

        Assert.False(promotion.IsCurrentlyRunning(DateOnly.FromDateTime(DateTime.Today)));
    }

    [Fact]
    public void IsCurrentlyRunning_ActiveWithinDateRange_IsTrue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var promotion = new Promotion
        {
            Title = "Current promotion",
            Description = "test",
            DiscountPercent = 10,
            StartDate = today.AddDays(-1),
            EndDate = today.AddDays(1),
            IsActive = true
        };

        Assert.True(promotion.IsCurrentlyRunning(today));
    }

    [Fact]
    public void IsCurrentlyRunning_InactiveEvenWithinDateRange_IsFalse()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var promotion = new Promotion
        {
            Title = "Disabled promotion",
            Description = "test",
            DiscountPercent = 10,
            StartDate = today.AddDays(-1),
            EndDate = today.AddDays(1),
            IsActive = false
        };

        Assert.False(promotion.IsCurrentlyRunning(today));
    }
}
