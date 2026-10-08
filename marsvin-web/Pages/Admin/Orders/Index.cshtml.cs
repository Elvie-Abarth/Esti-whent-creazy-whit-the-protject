using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Orders;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IOrderStore orders, IAuditLogStore audit, IUserAccountStore users, IEmailSender emailSender) : PageModel
{
    // Carriers' numbers are letters and digits, sometimes with a dash or space.
    private static readonly Regex TrackingNumberPattern = new("^[A-Za-z0-9 -]{4,50}$", RegexOptions.Compiled);

    public IReadOnlyList<Order> Items { get; private set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? ToastMessage { get; set; }

    public void OnGet() => Items = orders.GetAllOrders();

    // Packing and handing over orders is everyday shop-floor work, so an
    // Employee can move an order along too, not just an Admin.
    public async Task<IActionResult> OnPostSetStatusAsync(int orderId, OrderStatus status, string? trackingNumber)
    {
        // The select only offers the real values, but a number outside the
        // enum binds just as happily from a hand-made POST.
        if (!Enum.IsDefined(status)) return BadRequest();

        trackingNumber = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim();
        if (trackingNumber is not null && !TrackingNumberPattern.IsMatch(trackingNumber))
        {
            ErrorMessage = new Bilingual(
                "Track & trace-nummeret må kun bestå af bogstaver, tal, mellemrum og bindestreg (4-50 tegn).",
                "The tracking number may only contain letters, digits, spaces and dashes (4-50 characters).");
            return RedirectToPage();
        }

        var order = orders.FindById(orderId);
        if (order is null) return NotFound();
        // A pickup order has no parcel to track, whatever the POST says.
        if (order.DeliveryMethod != DeliveryMethod.Shipping) trackingNumber = null;

        // Cancelling isn't just another status: it also puts the items back
        // on sale, and it's refused once the order has been sent.
        var changed = status == OrderStatus.Cancelled
            ? orders.Cancel(orderId, OrderStatus.Processing)
            : orders.UpdateStatus(orderId, status, trackingNumber);
        if (!changed)
        {
            ErrorMessage = status == OrderStatus.Cancelled
                ? new Bilingual(
                    $"Ordre #{orderId} kan ikke annulleres - den er allerede sendt eller annulleret.",
                    $"Order #{orderId} can't be cancelled - it has already been sent or cancelled.")
                : new Bilingual(
                    $"Ordre #{orderId} er annulleret og kan ikke ændres.",
                    $"Order #{orderId} is cancelled and can't be changed.");
            return RedirectToPage();
        }

        if (order.Status != status)
        {
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Order.StatusChanged",
                $"#{orderId}: {order.Status} -> {status}");
            // The buyer hears about it by email - see OrderEmails for why.
            await OrderEmails.SendStatusUpdateAsync(emailSender, users, orders.FindById(orderId)!);
            ToastMessage = new Bilingual(
                $"Ordre #{orderId} er opdateret, og kunden har fået besked på e-mail.",
                $"Order #{orderId} was updated and the customer has been emailed.");
        }
        else if (order.TrackingNumber != trackingNumber)
        {
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Order.TrackingChanged",
                $"#{orderId}: {trackingNumber ?? "(removed)"}");
        }
        return RedirectToPage();
    }
}
