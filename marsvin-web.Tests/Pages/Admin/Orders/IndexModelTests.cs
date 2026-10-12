using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Admin.Orders;
using MarsvinWebExample.Tests.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Mvc;

namespace MarsvinWebExample.Tests.Pages.Admin.Orders;

/// <summary>
/// Staff moving an order along on /Admin/Orders: what a status change saves,
/// audits and emails, and what a hand-made POST can't get away with.
/// </summary>
[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlAuditLogStore _audit = new(fixture.ConnectionString);
    private readonly RecordingEmailSender _email = new();

    private ApplicationUser NewUser(UserRole role, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{role}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, role);
        return _users.FindByEmail(email)!;
    }

    private IndexModel MakeModelSignedInAs(ApplicationUser staff) =>
        new(_orders, _audit, _users, _email) { PageContext = TestAuth.ContextFor(staff.UserId, staff.Role.ToString()) };

    private Order PlaceOrder(ApplicationUser customer, bool shipping = false)
    {
        _cart.AddOrIncrement(customer.UserId, 104, 1);
        var result = shipping
            ? _orders.Checkout(customer.UserId, DeliveryMethod.Shipping, "Testvej 1, 6700 Esbjerg", ShippingCarrier.Gls)
            : _orders.Checkout(customer.UserId);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Order!;
    }

    private AuditLogEntry? AuditEntry(string action, int orderId) =>
        _audit.GetRecent(200).FirstOrDefault(e => e.Action == action && e.Details.StartsWith($"#{orderId}:"));

    [Fact]
    public async Task SetStatus_ByAnEmployee_SavesIt_AuditsWhoDidIt_AndEmailsTheBuyer()
    {
        var customer = NewUser(UserRole.Customer);
        var employee = NewUser(UserRole.Employee);
        var order = PlaceOrder(customer);
        var model = MakeModelSignedInAs(employee);

        var result = await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Processing, trackingNumber: null);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(OrderStatus.Processing, _orders.FindById(order.OrderId)!.Status);
        var entry = AuditEntry("Order.StatusChanged", order.OrderId);
        Assert.NotNull(entry);
        Assert.Equal(employee.UserId, entry!.ActorUserId);
        Assert.Contains("Placed -> Processing", entry.Details);
        var mail = Assert.Single(_email.Sent);
        Assert.Equal(customer.Email, mail.ToEmail);
        Assert.NotNull(model.ToastMessage);
    }

    [Fact]
    public async Task SetStatus_WithAValueOutsideTheEnum_IsABadRequest_AndChangesNothing()
    {
        var order = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        var result = await model.OnPostSetStatusAsync(order.OrderId, (OrderStatus)99, trackingNumber: null);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(OrderStatus.Placed, _orders.FindById(order.OrderId)!.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task SetStatus_ForAnOrderThatDoesNotExist_Is404()
    {
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        var result = await model.OnPostSetStatusAsync(int.MaxValue, OrderStatus.Processing, trackingNumber: null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("AB1")]                              // too short
    [InlineData("'; DROP TABLE Orders;--")]
    public async Task SetStatus_WithATrackingNumberThatIsNotLettersAndDigits_IsRefused(string trackingNumber)
    {
        var order = PlaceOrder(NewUser(UserRole.Customer), shipping: true);
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        var result = await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Sent, trackingNumber);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(model.ErrorMessage);
        var unchanged = _orders.FindById(order.OrderId)!;
        Assert.Equal(OrderStatus.Placed, unchanged.Status);
        Assert.Null(unchanged.TrackingNumber);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task SetStatus_Sent_OnAShippedOrder_StoresTheTrackingNumber_AndPutsItInTheMail()
    {
        var order = PlaceOrder(NewUser(UserRole.Customer), shipping: true);
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Sent, "  GLS-12345678  ");

        var updated = _orders.FindById(order.OrderId)!;
        Assert.Equal(OrderStatus.Sent, updated.Status);
        Assert.Equal("GLS-12345678", updated.TrackingNumber);
        Assert.Contains("GLS-12345678", Assert.Single(_email.Sent).Body);
    }

    [Fact]
    public async Task SetStatus_OnAPickupOrder_IgnoresATrackingNumberFromThePost()
    {
        var order = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Sent, "GLS-12345678");

        var updated = _orders.FindById(order.OrderId)!;
        Assert.Equal(OrderStatus.Sent, updated.Status);
        Assert.Null(updated.TrackingNumber);
    }

    [Fact]
    public async Task SetStatus_Cancelled_AfterTheOrderWasSent_IsRefused_AndNobodyIsEmailed()
    {
        var order = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));
        await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Sent, trackingNumber: null);
        _email.Sent.Clear();

        var again = MakeModelSignedInAs(NewUser(UserRole.Admin));
        await again.OnPostSetStatusAsync(order.OrderId, OrderStatus.Cancelled, trackingNumber: null);

        Assert.Equal(OrderStatus.Sent, _orders.FindById(order.OrderId)!.Status);
        Assert.NotNull(again.ErrorMessage);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task SetStatus_Cancelled_BeforeItIsSent_CancelsTheOrder_AndPutsTheItemBackInStock()
    {
        var catalog = new SqlCatalog(fixture.ConnectionString);
        var stockBefore = catalog.Accessories.Single(p => p.ProductId == 104).StockQuantity;
        var order = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Cancelled, trackingNumber: null);

        Assert.Equal(OrderStatus.Cancelled, _orders.FindById(order.OrderId)!.Status);
        Assert.Equal(stockBefore, catalog.Accessories.Single(p => p.ProductId == 104).StockQuantity);
        Assert.NotNull(AuditEntry("Order.StatusChanged", order.OrderId));
    }

    [Fact]
    public async Task SetStatus_WithTheSameStatus_SendsNoMailAndAuditsNoStatusChange()
    {
        var order = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Admin));

        await model.OnPostSetStatusAsync(order.OrderId, OrderStatus.Placed, trackingNumber: null);

        Assert.Empty(_email.Sent);
        Assert.Null(AuditEntry("Order.StatusChanged", order.OrderId));
    }

    [Fact]
    public void OnGet_ListsOrdersFromEveryCustomer()
    {
        var first = PlaceOrder(NewUser(UserRole.Customer));
        var second = PlaceOrder(NewUser(UserRole.Customer));
        var model = MakeModelSignedInAs(NewUser(UserRole.Employee));

        model.OnGet();

        Assert.Contains(model.Items, o => o.OrderId == first.OrderId);
        Assert.Contains(model.Items, o => o.OrderId == second.OrderId);
    }
}
