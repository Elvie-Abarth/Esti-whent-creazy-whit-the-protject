using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Orders;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IOrderStore orders, IAuditLogStore audit) : PageModel
{
    public IReadOnlyList<Order> Items { get; private set; } = [];

    public void OnGet() => Items = orders.GetAllOrders();

    // Packing and handing over orders is everyday shop-floor work, so an
    // Employee can move an order along too, not just an Admin.
    public IActionResult OnPostSetStatus(int orderId, OrderStatus status)
    {
        // The select only offers the real values, but a number outside the
        // enum binds just as happily from a hand-made POST.
        if (!Enum.IsDefined(status)) return BadRequest();

        var order = orders.FindById(orderId);
        if (order is null) return NotFound();

        if (order.Status != status && orders.UpdateStatus(orderId, status))
        {
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Order.StatusChanged",
                $"#{orderId}: {order.Status} -> {status}");
        }
        return RedirectToPage();
    }
}
