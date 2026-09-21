using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace MarsvinWebExample.Tests.Pages.Account;

[Collection("SqlCatalog collection")]
public class ProfileModelTests(SqlCatalogFixture fixture)
{
    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlOrderStore _orders = new(fixture.ConnectionString);
    private readonly SqlCartStore _cart = new(fixture.ConnectionString);
    private readonly SqlCatalog _catalog = new(fixture.ConnectionString);
    private readonly SqlShiftStore _shifts = new(fixture.ConnectionString);
    private readonly SqlTimeOffRequestStore _timeOffRequests = new(fixture.ConnectionString);
    private const string OriginalPassword = "OriginalPass123!";

    // Same RecordingAuthenticationService/RecordingEmailSender as LoginModelTests
    // (same namespace) - lets OnPostAsync's HttpContext.SignInAsync succeed
    // without the real auth middleware pipeline, and lets a test see what it
    // signed in as or what it would have emailed.
    private (ProfileModel Model, RecordingAuthenticationService Auth, RecordingEmailSender Email) MakeModel(int userId, string role = "Customer")
    {
        var services = new ServiceCollection();
        var auth = new RecordingAuthenticationService();
        services.AddSingleton<IAuthenticationService>(auth);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)
                ],
                authenticationType: "Test"))
        };

        var email = new RecordingEmailSender();
        var model = new ProfileModel(_users, _orders, _cart, _catalog, _shifts, _timeOffRequests, email)
            { PageContext = new PageContext { HttpContext = httpContext } };
        return (model, auth, email);
    }

    private ApplicationUser NewCustomer([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        var hasher = new PasswordHasher<ApplicationUser>();
        _users.CreateUser(email, hasher.HashPassword(null!, OriginalPassword), caller, UserRole.Customer);
        return _users.FindByEmail(email)!;
    }

    private ApplicationUser NewStaff(UserRole role, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com";
        _users.CreateUser(email, "hash", caller, role);
        return _users.FindByEmail(email)!;
    }

    [Fact]
    public void OnGet_LoadsCurrentDisplayNameAndEmail()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);

        model.OnGet();

        Assert.Equal(user.DisplayName, model.Input.DisplayName);
        Assert.Equal(user.Email, model.Input.Email);
    }

    [Fact]
    public async Task OnPostAsync_WrongCurrentPassword_DoesNotUpdateAndShowsError()
    {
        var user = NewCustomer();
        var (model, auth, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = "New Name",
            Email = user.Email,
            CurrentPassword = "WrongPassword!"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        // Keyed to "Input.CurrentPassword" (matching asp-for's generated
        // name) - nameof(Input.CurrentPassword) evaluates to the bare
        // "CurrentPassword", which asp-validation-for would never match.
        Assert.True(model.ModelState.ContainsKey("Input.CurrentPassword"));
        Assert.Equal(user.DisplayName, _users.FindById(user.UserId)!.DisplayName);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_UpdatesNameAndEmailAndSignsInAgain()
    {
        var user = NewCustomer();
        var (model, auth, _) = MakeModel(user.UserId);
        var newEmail = $"updated-{Guid.NewGuid():N}@example.com";
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = "Updated Name",
            Email = newEmail,
            CurrentPassword = OriginalPassword
        };

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var updated = _users.FindById(user.UserId)!;
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal(newEmail, updated.Email);
        Assert.NotNull(auth.SignedInAs);
        Assert.Equal("Updated Name", auth.SignedInAs!.Identity!.Name);
    }

    [Fact]
    public async Task OnPostAsync_EmailChanged_NotifiesTheOldAddress()
    {
        // ASVS 2.5.5 - notified at the *old* address, the one place still
        // guaranteed to reach the real owner if this wasn't actually them.
        var user = NewCustomer();
        var originalEmail = user.Email;
        var (model, _, sentEmail) = MakeModel(user.UserId);
        var newEmail = $"updated-{Guid.NewGuid():N}@example.com";
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = newEmail,
            CurrentPassword = OriginalPassword
        };

        await model.OnPostAsync();

        var notification = Assert.Single(sentEmail.Sent);
        Assert.Equal(originalEmail, notification.ToEmail);
    }

    [Fact]
    public async Task OnPostAsync_PasswordChanged_NotifiesTheCurrentAddress()
    {
        var user = NewCustomer();
        var (model, _, sentEmail) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            CurrentPassword = OriginalPassword,
            NewPassword = "BrandNewPass456!",
            ConfirmNewPassword = "BrandNewPass456!"
        };

        await model.OnPostAsync();

        var notification = Assert.Single(sentEmail.Sent);
        Assert.Equal(user.Email, notification.ToEmail, ignoreCase: true); // ProfileModel normalizes email to lowercase before sending
    }

    [Fact]
    public async Task OnPostAsync_NameOnlyChanged_SendsNoNotificationEmail()
    {
        // Neither auth factor (email, password) changed - nothing to notify about.
        var user = NewCustomer();
        var (model, _, sentEmail) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = "Just A New Name",
            Email = user.Email,
            CurrentPassword = OriginalPassword
        };

        await model.OnPostAsync();

        Assert.Empty(sentEmail.Sent);
    }

    [Fact]
    public void OnPostStartTotpEnrollment_GeneratesAndStoresAPendingSecret()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);

        model.OnPostStartTotpEnrollment();

        var updated = _users.FindById(user.UserId)!;
        Assert.NotNull(updated.TotpSecret);
        Assert.False(updated.TotpEnabled);
    }

    [Fact]
    public void OnPostConfirmTotp_ValidCode_EnablesTotp()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);
        model.OnPostStartTotpEnrollment();
        var secret = _users.FindById(user.UserId)!.TotpSecret!;
        var code = TotpTestHelper.CurrentCode(secret);

        model.OnPostConfirmTotp(code);

        Assert.True(_users.FindById(user.UserId)!.TotpEnabled);
        Assert.NotNull(model.ToastMessage);
    }

    [Fact]
    public void OnPostConfirmTotp_WrongCode_DoesNotEnableAndShowsError()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);
        model.OnPostStartTotpEnrollment();

        model.OnPostConfirmTotp("000000");

        Assert.False(_users.FindById(user.UserId)!.TotpEnabled);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostDisableTotp_CorrectPassword_ClearsSecretAndDisables()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);
        model.OnPostStartTotpEnrollment();
        var secret = _users.FindById(user.UserId)!.TotpSecret!;
        model.OnPostConfirmTotp(TotpTestHelper.CurrentCode(secret));

        model.OnPostDisableTotp(OriginalPassword);

        var updated = _users.FindById(user.UserId)!;
        Assert.False(updated.TotpEnabled);
        Assert.Null(updated.TotpSecret);
    }

    [Fact]
    public void OnPostDisableTotp_WrongPassword_LeavesTotpEnabled()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);
        model.OnPostStartTotpEnrollment();
        var secret = _users.FindById(user.UserId)!.TotpSecret!;
        model.OnPostConfirmTotp(TotpTestHelper.CurrentCode(secret));

        model.OnPostDisableTotp("WrongPassword!");

        var updated = _users.FindById(user.UserId)!;
        Assert.True(updated.TotpEnabled);
        Assert.NotNull(updated.TotpSecret);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_EmailAlreadyUsedByAnotherAccount_ShowsErrorAndDoesNotUpdate()
    {
        var user = NewCustomer();
        var other = NewCustomer();
        var (model, auth, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = other.Email,
            CurrentPassword = OriginalPassword
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Null(auth.SignedInAs);
        Assert.True(model.ModelState.ContainsKey("Input.Email"));
        Assert.Equal(user.Email, _users.FindById(user.UserId)!.Email);
    }

    [Fact]
    public async Task OnPostAsync_NewPasswordProvided_ChangesPasswordHash()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            CurrentPassword = OriginalPassword,
            NewPassword = "BrandNewPass456!",
            ConfirmNewPassword = "BrandNewPass456!"
        };

        await model.OnPostAsync();

        var updated = _users.FindById(user.UserId)!;
        var hasher = new PasswordHasher<ApplicationUser>();
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, updated.PasswordHash, OriginalPassword));
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(updated, updated.PasswordHash, "BrandNewPass456!"));
    }

    [Fact]
    public async Task OnPostAsync_NoNewPassword_LeavesPasswordUnchanged()
    {
        var user = NewCustomer();
        var passwordHashBefore = _users.FindById(user.UserId)!.PasswordHash;
        var (model, _, _) = MakeModel(user.UserId);
        model.Input = new ProfileModel.InputModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            CurrentPassword = OriginalPassword
        };

        await model.OnPostAsync();

        Assert.Equal(passwordHashBefore, _users.FindById(user.UserId)!.PasswordHash);
    }

    [Fact]
    public void OnGet_CustomerWithOrders_PopulatesOrderHistory()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 1);
        _orders.Checkout(user.UserId);
        var (model, _, _) = MakeModel(user.UserId);

        model.OnGet();

        Assert.Single(model.Orders);
    }

    [Fact]
    public void OnGet_StaffAccount_NeverPopulatesOrderHistory()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId, role: "Admin");

        model.OnGet();

        Assert.Empty(model.Orders);
    }

    [Fact]
    public void OnPostReorder_AvailableAccessory_AddsItToTheCart()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 2);
        var order = _orders.Checkout(user.UserId).Order!;
        var (model, _, _) = MakeModel(user.UserId);

        model.OnPostReorder(order.OrderId);

        var line = Assert.Single(_cart.GetLines(user.UserId));
        Assert.Equal(104, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.NotNull(model.ToastMessage);
    }

    [Fact]
    public void OnPostReorder_AnimalInThatOrder_IsAlwaysSkipped()
    {
        // A throwaway animal, not one of the seeded ones (Pelle etc.) - this
        // test's checkout actually marks it Sold, and reusing a seeded animal
        // would permanently break every other test that assumes it's Available.
        var user = NewCustomer();
        var animal = new Animal
        {
            ProductId = 0,
            Name = $"ReorderTest-{Guid.NewGuid():N}",
            Description = "test",
            Breed = "test",
            Sex = Sex.Boar,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(-70)),
            Colour = "test",
            CoatPrimary = "#000000",
            CoatSecondary = "#ffffff",
            Status = AnimalStatus.Available
        };
        _catalog.CreateAnimal(animal);
        var created = _catalog.Animals.Single(a => a.Name == animal.Name);
        try
        {
            _cart.AddOrIncrement(user.UserId, created.ProductId, 1);
            var order = _orders.Checkout(user.UserId).Order!;
            var (model, _, _) = MakeModel(user.UserId);

            model.OnPostReorder(order.OrderId);

            Assert.Empty(_cart.GetLines(user.UserId));
            Assert.NotNull(model.ErrorMessage);
        }
        finally
        {
            _catalog.DeleteAnimal(created.ProductId);
        }
    }

    [Fact]
    public void OnPostReorder_OrderBelongingToAnotherUser_IsNotFoundNotLeaked()
    {
        var owner = NewCustomer();
        var stranger = NewCustomer();
        _cart.AddOrIncrement(owner.UserId, 104, 1);
        var order = _orders.Checkout(owner.UserId).Order!;
        var (model, _, _) = MakeModel(stranger.UserId);

        model.OnPostReorder(order.OrderId);

        Assert.Empty(_cart.GetLines(stranger.UserId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public void OnPostReorder_NonCustomerAccount_IsForbidden()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        var (model, _, _) = MakeModel(user.UserId, role: "Admin");

        var result = model.OnPostReorder(order.OrderId);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public void OnPostReorder_NowOutOfStock_SkipsWithoutThrowing()
    {
        var user = NewCustomer();
        const int productId = 109;
        var stock = _catalog.Accessories.Single(p => p.ProductId == productId).StockQuantity;
        _cart.AddOrIncrement(user.UserId, productId, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        // Deplete the remaining stock so the reorder can no longer be fulfilled.
        _catalog.UpdateStockQuantity(productId, 0);
        var (model, _, _) = MakeModel(user.UserId);

        model.OnPostReorder(order.OrderId);

        Assert.Empty(_cart.GetLines(user.UserId));
        Assert.NotNull(model.ErrorMessage);

        _catalog.UpdateStockQuantity(productId, stock - 1); // restore for other tests in this shared fixture
    }

    [Fact]
    public async Task OnPostDeleteAccountAsync_CorrectPassword_DeletesTheAccount()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);

        var result = await model.OnPostDeleteAccountAsync(OriginalPassword);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
        Assert.Null(_users.FindById(user.UserId));
        Assert.NotNull(model.ToastMessage);
    }

    [Fact]
    public async Task OnPostDeleteAccountAsync_WrongPassword_DoesNotDeleteAndShowsError()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);

        await model.OnPostDeleteAccountAsync("WrongPassword!");

        Assert.NotNull(_users.FindById(user.UserId));
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAccountAsync_NonCustomerAccount_IsForbiddenAndAccountRemains()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId, role: "Admin");

        var result = await model.OnPostDeleteAccountAsync(OriginalPassword);

        Assert.IsType<ForbidResult>(result);
        Assert.NotNull(_users.FindById(user.UserId));
    }

    [Fact]
    public async Task OnPostDeleteAccountAsync_WithPastOrders_KeepsTheOrderRowButOrphansIt()
    {
        var user = NewCustomer();
        _cart.AddOrIncrement(user.UserId, 104, 1);
        var order = _orders.Checkout(user.UserId).Order!;
        var (model, _, _) = MakeModel(user.UserId);

        await model.OnPostDeleteAccountAsync(OriginalPassword);

        Assert.Null(_users.FindById(user.UserId));
        // FindForUser requires a matching UserId, which is now null on the
        // order row, so it correctly can't find it "for" the deleted user
        // either way - check the row itself still exists, via GetOrdersForUser
        // for a *different*, freshly-made customer who never placed it: if the
        // row were gone, this proves nothing; the real check is the raw count.
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM dbo.Orders WHERE OrderId = @OrderId;";
        command.Parameters.AddWithValue("@OrderId", order.OrderId);
        var value = command.ExecuteScalar();
        Assert.NotNull(value); // the row exists at all
        Assert.True(value is DBNull); // ...but UserId on it is now NULL, not just absent
    }

    [Fact]
    public void OnGet_Employee_ComputesTotalWorkHoursFromShifts()
    {
        var staff = NewStaff(UserRole.Employee);
        _shifts.Create(staff.UserId, new DateTime(2026, 3, 5, 14, 0, 0), new DateTime(2026, 3, 5, 18, 0, 0), null);
        _shifts.Create(staff.UserId, new DateTime(2026, 3, 6, 10, 0, 0), new DateTime(2026, 3, 6, 14, 0, 0), null);
        var (model, _, _) = MakeModel(staff.UserId, role: "Employee");

        model.OnGet();

        Assert.Equal(8.0, model.TotalWorkHours);
    }

    [Fact]
    public void OnGet_Admin_AlsoComputesTotalWorkHours()
    {
        var staff = NewStaff(UserRole.Admin);
        _shifts.Create(staff.UserId, new DateTime(2026, 3, 5, 14, 0, 0), new DateTime(2026, 3, 5, 18, 0, 0), null);
        var (model, _, _) = MakeModel(staff.UserId, role: "Admin");

        model.OnGet();

        Assert.Equal(4.0, model.TotalWorkHours);
    }

    [Fact]
    public void OnGet_Customer_NeverComputesWorkHours()
    {
        var user = NewCustomer();
        var (model, _, _) = MakeModel(user.UserId);

        model.OnGet();

        Assert.Equal(0.0, model.TotalWorkHours);
    }

    [Fact]
    public void OnGet_Employee_PopulatesTheirOwnTimeOffRequests()
    {
        var staff = NewStaff(UserRole.Employee);
        _timeOffRequests.Create(staff.UserId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "Ferie");
        var (model, _, _) = MakeModel(staff.UserId, role: "Employee");

        model.OnGet();

        var request = Assert.Single(model.MyTimeOffRequests);
        Assert.Equal("Ferie", request.Reason);
        Assert.Equal(TimeOffStatus.Pending, request.Status);
    }

    [Fact]
    public async Task OnPostRequestTimeOffAsync_Employee_CreatesRequestAndEmailsActiveAdmins()
    {
        var staff = NewStaff(UserRole.Employee);
        var admin = NewStaff(UserRole.Admin);
        var inactiveAdmin = NewStaff(UserRole.Admin);
        _users.SetActive(inactiveAdmin.UserId, false);
        try
        {
            var (model, _, sentEmail) = MakeModel(staff.UserId, role: "Employee");

            var result = await model.OnPostRequestTimeOffAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), "Ferie");

            Assert.IsType<RedirectToPageResult>(result);
            var request = Assert.Single(_timeOffRequests.GetForUser(staff.UserId));
            Assert.Equal("Ferie", request.Reason);
            Assert.Contains(sentEmail.Sent, e => e.ToEmail == admin.Email);
            Assert.DoesNotContain(sentEmail.Sent, e => e.ToEmail == inactiveAdmin.Email);
        }
        finally
        {
            _users.SetActive(inactiveAdmin.UserId, true);
        }
    }

    [Fact]
    public async Task OnPostRequestTimeOffAsync_NonEmployee_IsForbiddenAndCreatesNothing()
    {
        var admin = NewStaff(UserRole.Admin);
        var (model, _, sentEmail) = MakeModel(admin.UserId, role: "Admin");

        var result = await model.OnPostRequestTimeOffAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3), null);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_timeOffRequests.GetForUser(admin.UserId));
        Assert.Empty(sentEmail.Sent);
    }

    [Fact]
    public async Task OnPostRequestTimeOffAsync_EndBeforeStart_ShowsErrorAndCreatesNothing()
    {
        var staff = NewStaff(UserRole.Employee);
        var (model, _, sentEmail) = MakeModel(staff.UserId, role: "Employee");

        await model.OnPostRequestTimeOffAsync(new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 1), null);

        Assert.NotNull(model.ErrorMessage);
        Assert.Empty(_timeOffRequests.GetForUser(staff.UserId));
        Assert.Empty(sentEmail.Sent);
    }
}
