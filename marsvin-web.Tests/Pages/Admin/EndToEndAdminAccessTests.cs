using System.Net;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MarsvinWebExample.Tests.Pages.Admin;

/// <summary>
/// Who gets into which staff page, over real HTTP - the role checks are
/// attributes on the page models, so only a request through the real
/// pipeline shows whether they hold. Every page under /Admin is tried as a
/// visitor who isn't logged in, as a customer, as an employee and as an
/// admin, including a POST an employee isn't allowed to make.
/// </summary>
[Collection("WebApp collection")]
public class EndToEndAdminAccessTests(MarsvinWebAppFactory factory)
{
    // Open to both staff roles.
    private static readonly string[] StaffPages =
    [
        "/Admin/Index", "/Admin/Shop/Index", "/Admin/Stock/Index", "/Admin/Orders/Index",
        "/Admin/Messages/Index", "/Admin/Donations/Index", "/Admin/Schedule/Index"
    ];

    // Admin only: everything that adds, edits or deletes, accounts, and the audit log.
    private static readonly string[] AdminOnlyPages =
    [
        "/Admin/Products/Index", "/Admin/Products/Edit/0", "/Admin/Animals/Index", "/Admin/Animals/Edit/0",
        "/Admin/Promotions/Index", "/Admin/Promotions/Edit/0", "/Admin/Users/Index", "/Admin/AuditLog/Index"
    ];

    private static readonly string[] AllAdminPages = [.. StaffPages, .. AdminOnlyPages];

    public static TheoryData<string> AllAdminPagesData => new(AllAdminPages);

    private HttpClient MakeClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    private static async Task SignInAs(HttpClient client, CookieJar jar, string email, string password)
    {
        var (_, _, loginToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Login");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Login", new()
        {
            ["__RequestVerificationToken"] = loginToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
    }

    private static Task SignInAsEmployee(HttpClient client, CookieJar jar) =>
        SignInAs(client, jar, "employee@marsvin.dk", "Employee123!");

    private static Task SignInAsAdmin(HttpClient client, CookieJar jar) =>
        SignInAs(client, jar, "admin@marsvin.dk", "Admin123!");

    private static async Task SignInAsNewCustomer(HttpClient client, CookieJar jar)
    {
        var email = $"admin-access-{Guid.NewGuid():N}@example.com";
        var (_, _, registerToken) = await HttpTestHelpers.GetWithToken(client, jar, "/Account/Register");
        await HttpTestHelpers.PostForm(client, jar, "/Account/Register", new()
        {
            ["__RequestVerificationToken"] = registerToken,
            ["Input.Email"] = email,
            ["Input.DisplayName"] = "Nysgerrig Kunde",
            ["Input.Password"] = "SomePass123!",
            ["Input.ConfirmPassword"] = "SomePass123!"
        });
        await HttpTestHelpers.CompleteEmailConfirmation(client, jar, email);
    }

    private static void AssertSentTo(HttpResponseMessage response, string expectedPath)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(expectedPath, response.Headers.Location!.ToString());
    }

    [Theory]
    [MemberData(nameof(AllAdminPagesData))]
    public async Task AVisitorWhoIsNotLoggedIn_IsSentToLogin(string path)
    {
        var response = await MakeClient().GetAsync(path);

        AssertSentTo(response, "/Account/Login");
    }

    [Fact]
    public async Task ACustomer_IsRefusedOnEveryAdminPage()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsNewCustomer(client, jar);

        foreach (var path in AllAdminPages)
        {
            AssertSentTo(await HttpTestHelpers.Get(client, jar, path), "/Account/AccessDenied");
        }
    }

    [Fact]
    public async Task AnEmployee_GetsTheStaffPages_ButIsRefusedOnTheAdminOnlyOnes()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsEmployee(client, jar);

        foreach (var path in StaffPages)
        {
            var response = await HttpTestHelpers.Get(client, jar, path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} gave {(int)response.StatusCode} for an employee");
        }
        foreach (var path in AdminOnlyPages)
        {
            AssertSentTo(await HttpTestHelpers.Get(client, jar, path), "/Account/AccessDenied");
        }
    }

    [Fact]
    public async Task AnAdmin_GetsEveryAdminPage()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsAdmin(client, jar);

        foreach (var path in AllAdminPages)
        {
            var response = await HttpTestHelpers.Get(client, jar, path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} gave {(int)response.StatusCode} for an admin");
        }
    }

    [Theory]
    [InlineData("/Admin/Products/Index?handler=Delete", "productId", "104")]
    [InlineData("/Admin/Animals/Index?handler=Delete", "productId", "1")]
    [InlineData("/Admin/Promotions/Index?handler=Delete", "promotionId", "1")]
    [InlineData("/Admin/Users/Index?handler=UpdateRole", "userId", "1")]
    public async Task AnEmployee_PostingStraightToAnAdminOnlyHandler_IsRefused(string path, string field, string value)
    {
        // Hiding the button isn't the protection: the employee has a valid
        // session and a valid antiforgery token (from a page they may see),
        // and posts to the handler directly.
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsEmployee(client, jar);
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Admin/Stock/Index");
        var catalog = new SqlCatalog(MarsvinWebAppFactory.ConnectionString);
        var accessoriesBefore = catalog.Accessories.Count;
        var animalsBefore = catalog.Animals.Count;

        var response = await HttpTestHelpers.PostForm(client, jar, path, new()
        {
            ["__RequestVerificationToken"] = token,
            [field] = value,
            ["role"] = "Admin"
        });

        AssertSentTo(response, "/Account/AccessDenied");
        Assert.Equal(accessoriesBefore, catalog.Accessories.Count);
        Assert.Equal(animalsBefore, catalog.Animals.Count);
        var employee = new SqlUserAccountStore(MarsvinWebAppFactory.ConnectionString).FindByEmail("employee@marsvin.dk")!;
        Assert.Equal(UserRole.Employee, employee.Role);
    }

    [Fact]
    public async Task AnEmployee_CanChangeStock_AndTheChangeShowsInTheAuditLogForAnAdmin()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsEmployee(client, jar);
        var catalog = new SqlCatalog(MarsvinWebAppFactory.ConnectionString);
        var product = catalog.Accessories.Single(p => p.ProductId == 104);
        var (_, _, token) = await HttpTestHelpers.GetWithToken(client, jar, "/Admin/Stock/Index");

        try
        {
            var response = await HttpTestHelpers.PostForm(client, jar, "/Admin/Stock/Index?handler=UpdateStock", new()
            {
                ["__RequestVerificationToken"] = token,
                ["productId"] = "104",
                ["stockQuantity"] = (product.StockQuantity + 7).ToString()
            });

            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            Assert.Equal(product.StockQuantity + 7, catalog.Accessories.Single(p => p.ProductId == 104).StockQuantity);

            var adminClient = MakeClient();
            var adminJar = new CookieJar();
            await SignInAsAdmin(adminClient, adminJar);
            var auditHtml = WebUtility.HtmlDecode(
                await (await HttpTestHelpers.Get(adminClient, adminJar, "/Admin/AuditLog/Index")).Content.ReadAsStringAsync());
            Assert.Contains($"{product.StockQuantity} -> {product.StockQuantity + 7}", auditHtml);
        }
        finally
        {
            catalog.UpdateStockQuantity(104, product.StockQuantity);
        }
    }

    [Fact]
    public async Task AStaffPost_WithoutAnAntiforgeryToken_IsRejected()
    {
        var client = MakeClient();
        var jar = new CookieJar();
        await SignInAsEmployee(client, jar);
        var catalog = new SqlCatalog(MarsvinWebAppFactory.ConnectionString);
        var before = catalog.Accessories.Single(p => p.ProductId == 104).StockQuantity;

        var response = await HttpTestHelpers.PostForm(client, jar, "/Admin/Stock/Index?handler=UpdateStock", new()
        {
            ["productId"] = "104",
            ["stockQuantity"] = "0"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, catalog.Accessories.Single(p => p.ProductId == 104).StockQuantity);
    }
}
