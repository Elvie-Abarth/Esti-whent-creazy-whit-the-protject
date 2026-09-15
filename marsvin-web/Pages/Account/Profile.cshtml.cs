using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Account;

// Open to every signed-in role, not just Customer - clicking your own name in
// the header should let you manage your own account whether you're a
// customer, an employee, or the admin themself.
[Authorize]
public class ProfileModel(
    IUserAccountStore users, IOrderStore orders, ICartStore cart, ICatalog catalog,
    IShiftStore shifts, ITimeOffRequestStore timeOffRequests, IEmailSender emailSender) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    // Only Customers ever place orders (Cart/Checkout are Customer-only), so
    // this stays empty for staff accounts rather than querying for nothing.
    public IReadOnlyList<Order> Orders { get; private set; } = [];

    // Employee and Admin both get scheduled shifts (see Admin/Schedule), so
    // both see their own total here - it's the day-off *requests* below that
    // stay Employee-only, since an Admin has no one to request from.
    public double TotalWorkHours { get; private set; }

    public IReadOnlyList<TimeOffRequest> MyTimeOffRequests { get; private set; } = [];

    public void OnGet()
    {
        var user = users.FindById(this.CurrentUserId())!;
        Input.DisplayName = user.DisplayName;
        Input.Email = user.Email;

        if (User.IsInRole("Customer"))
        {
            Orders = orders.GetOrdersForUser(this.CurrentUserId());
        }

        if (User.IsInRole("Employee") || User.IsInRole("Admin"))
        {
            TotalWorkHours = shifts.GetForUser(this.CurrentUserId()).Sum(s => (s.EndAt - s.StartAt).TotalHours);
        }

        if (User.IsInRole("Employee"))
        {
            MyTimeOffRequests = timeOffRequests.GetForUser(this.CurrentUserId());
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = users.FindById(this.CurrentUserId())!;
        var hasher = new PasswordHasher<ApplicationUser>();

        // Changing your email or password is exactly the kind of thing that
        // shouldn't be possible from a hijacked, still-logged-in browser tab
        // alone - re-check the current password every time, the same way a
        // bank or email provider would before letting you change account details.
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, Input.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(Input.CurrentPassword), "Forkert adgangskode.");
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var updated = users.UpdateProfile(this.CurrentUserId(), Input.DisplayName.Trim(), email);
        if (!updated)
        {
            ModelState.AddModelError(nameof(Input.Email), "Der findes allerede en konto med den e-mail.");
            return Page();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            users.UpdatePassword(this.CurrentUserId(), hasher.HashPassword(user, Input.NewPassword));
        }

        // The auth cookie's claims (name, email) were fixed at login - refresh
        // them now, otherwise the header would keep showing the old name/email
        // until the next login even though the database is already updated.
        var refreshed = users.FindById(this.CurrentUserId())!;
        await this.SignInAsync(refreshed);

        ToastMessage = new Bilingual("Dine oplysninger er opdateret.", "Your details have been updated.");
        return RedirectToPage();
    }

    // Re-adds a past order's items to the current cart. FindForUser (not a raw
    // "get order by id") is what keeps this IDOR-safe - it only returns the
    // order if it actually belongs to the signed-in user, so an orderId for
    // someone else's order just looks like "not found", never leaks their
    // order contents. Guinea pigs are never actually re-orderable (the specific
    // animal is Sold, not restocked) - CanBeAddedToCart already says so, so
    // they always land in "couldn't add again" without needing special-casing.
    public IActionResult OnPostReorder(int orderId)
    {
        if (!User.IsInRole("Customer")) return Forbid();

        var order = orders.FindForUser(orderId, this.CurrentUserId());
        if (order is null)
        {
            ErrorMessage = new Bilingual("Ordren blev ikke fundet.", "The order wasn't found.");
            return RedirectToPage();
        }

        // Danish and English name lists tracked in parallel (not just one
        // list re-used for both toast halves) - an animal's name has no
        // English form to fall back to (it's a proper noun, same in both
        // languages), but an accessory's does, and re-using item.ProductName
        // (always the Danish snapshot) for the English half was exactly the
        // bug that showed a Danish product name inside an otherwise-English
        // "added to the cart" toast.
        var added = new List<string>();
        var addedEn = new List<string>();
        var skipped = new List<string>();
        var skippedEn = new List<string>();
        var currentLines = cart.GetLines(this.CurrentUserId());

        foreach (var item in order.Items)
        {
            // FindProduct instead of FindAnimal-then-Accessories.FirstOrDefault -
            // one lookup instead of two full-table loads per line.
            var product = catalog.FindProduct(item.ProductId);

            // A guinea pig is never actually re-orderable (the specific
            // animal is Sold, not restocked) - CanBeAddedToCart already says
            // so for a StockProduct, but an Animal has no such check to call,
            // so it's excluded here instead.
            if (product is not StockProduct stockProduct)
            {
                skipped.Add(item.ProductName);
                skippedEn.Add(item.ProductName);
                continue;
            }

            var nameEn = stockProduct.NameEn ?? stockProduct.Name;

            // Same reasoning as Cart/Index.OnPostAdd: checked against what's
            // already in the cart plus what's being re-added, not the
            // re-added quantity alone.
            var alreadyInCart = currentLines.FirstOrDefault(l => l.ProductId == item.ProductId)?.Quantity ?? 0;
            if (!stockProduct.CanBeAddedToCart(alreadyInCart + item.Quantity))
            {
                skipped.Add(item.ProductName);
                skippedEn.Add(nameEn);
                continue;
            }

            cart.AddOrIncrement(this.CurrentUserId(), item.ProductId, item.Quantity);
            added.Add(item.ProductName);
            addedEn.Add(nameEn);
        }

        if (added.Count > 0)
        {
            ToastMessage = new Bilingual(
                $"{string.Join(", ", added)} lagt i kurven.",
                $"{string.Join(", ", addedEn)} added to the cart.");
        }
        if (skipped.Count > 0)
        {
            ErrorMessage = new Bilingual(
                $"Kunne ikke tilføjes igen: {string.Join(", ", skipped)}.",
                $"Couldn't be added again: {string.Join(", ", skippedEn)}.");
        }

        return RedirectToPage();
    }

    // Self-service day-off request - Employee only, since an Admin approves
    // these rather than requesting from themselves. Every active Admin gets
    // emailed so the request doesn't just sit unseen until someone happens
    // to open Admin/Schedule.
    public async Task<IActionResult> OnPostRequestTimeOffAsync(DateOnly startDate, DateOnly endDate, string? reason)
    {
        if (!User.IsInRole("Employee")) return Forbid();

        if (endDate < startDate)
        {
            ErrorMessage = new Bilingual(
                "Slutdatoen skal være efter startdatoen.",
                "The end date must be after the start date.");
            return RedirectToPage();
        }
        // Matches the maxlength on the form field and dbo.TimeOffRequests.Reason
        // (NVARCHAR(500)) - the HTML attribute is only a hint, not enforcement,
        // so an over-length POST needs the same limit checked here too, rather
        // than reaching the database and failing there instead.
        if (reason is { Length: > 500 })
        {
            ErrorMessage = new Bilingual(
                "Begrundelsen må højst fylde 500 tegn.",
                "The reason can be at most 500 characters.");
            return RedirectToPage();
        }

        var requester = users.FindById(this.CurrentUserId())!;
        timeOffRequests.Create(this.CurrentUserId(), startDate, endDate, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());

        var admins = users.GetAll().Where(u => u.Role == UserRole.Admin && u.IsActive);
        foreach (var admin in admins)
        {
            await emailSender.SendAsync(admin.Email, "Ny ferieanmodning fra " + requester.DisplayName,
                $"""
                Hej {admin.DisplayName},

                {requester.DisplayName} har anmodet om fri fra {startDate:d MMM yyyy} til {endDate:d MMM yyyy}.
                {(string.IsNullOrWhiteSpace(reason) ? "" : $"Begrundelse: {reason.Trim()}")}

                Godkend eller afvis anmodningen under Vagtplan i personaleområdet.

                Venlig hilsen
                Marsvin
                """);
        }

        ToastMessage = new Bilingual("Din anmodning om fri er sendt.", "Your day-off request has been sent.");
        return RedirectToPage();
    }

    // The GDPR right to erasure this page's own Privatliv text promises -
    // a customer doesn't have to wait for the 2-year inactivity job or ask
    // an admin, they can delete their own account outright. Staff accounts
    // are managed by an admin instead (see Admin/Users), not self-service,
    // so this is deliberately Customer-only.
    public async Task<IActionResult> OnPostDeleteAccountAsync(string currentPassword)
    {
        if (!User.IsInRole("Customer")) return Forbid();

        var user = users.FindById(this.CurrentUserId())!;
        var hasher = new PasswordHasher<ApplicationUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            ErrorMessage = new Bilingual(
                "Forkert adgangskode - kontoen blev ikke slettet.",
                "Wrong password - the account wasn't deleted.");
            return RedirectToPage();
        }

        // Same DeleteUser as an admin uses or the inactivity job runs - cart
        // cleared, past orders kept but orphaned, never destroyed.
        users.DeleteUser(this.CurrentUserId());
        ToastMessage = new Bilingual("Din konto og dine data er slettet.", "Your account and data have been deleted.");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string DisplayName { get; set; } = "";

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = "";

        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string? ConfirmNewPassword { get; set; }

        [Required, DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";
    }
}
