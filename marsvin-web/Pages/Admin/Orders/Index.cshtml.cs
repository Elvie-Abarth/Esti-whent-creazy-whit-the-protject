using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Orders;

[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IOrderStore orders) : PageModel
{
    public IReadOnlyList<Order> Items { get; private set; } = [];

    public void OnGet() => Items = orders.GetAllOrders();
}
