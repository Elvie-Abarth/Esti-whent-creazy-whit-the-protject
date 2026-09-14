using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Cart;

[Authorize(Roles = "Customer")]
public class ConfirmationModel(IOrderStore orders) : PageModel
{
    public Order Order { get; private set; } = null!;

    public IActionResult OnGet(int orderId)
    {
        // FindForUser only returns the order if it belongs to the current
        // user - without that check, a customer could view anyone's order
        // just by changing the orderId in the URL.
        var order = orders.FindForUser(orderId, this.CurrentUserId());
        if (order is null) return NotFound();

        Order = order;
        return Page();
    }
}
