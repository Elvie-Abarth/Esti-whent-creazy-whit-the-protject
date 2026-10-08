using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Messages;

// Customer messages carry names and email addresses - staff only.
[Authorize(Roles = "Admin,Employee")]
public class IndexModel(IInboxStore inbox) : PageModel
{
    public IReadOnlyList<ContactMessage> Items { get; private set; } = [];

    public void OnGet() => Items = inbox.GetContactMessages(200);
}
