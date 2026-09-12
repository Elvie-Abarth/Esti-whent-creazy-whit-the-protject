using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Products;

[Authorize(Roles = "Admin")]
public class EditModel(ICatalog catalog, ICatalogAdmin catalogAdmin) : PageModel
{
    public int ProductId { get; set; }
    public bool IsNew => ProductId == 0;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet(int id)
    {
        ProductId = id;
        if (id == 0) return Page();

        var item = catalog.Accessories.FirstOrDefault(p => p.ProductId == id);
        if (item is null) return NotFound();

        Input = new InputModel
        {
            Name = item.Name,
            NameEn = item.NameEn,
            Description = item.Description,
            DescriptionEn = item.DescriptionEn,
            Sku = item.Sku,
            Category = item.Category,
            Price = item.Price,
            StockQuantity = item.StockQuantity,
            Unit = item.Unit
        };
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        ProductId = id;
        if (!ModelState.IsValid) return Page();

        var product = new StockProduct
        {
            ProductId = id,
            Name = Input.Name,
            NameEn = Input.NameEn,
            Description = Input.Description,
            DescriptionEn = Input.DescriptionEn,
            Sku = Input.Sku,
            Category = Input.Category,
            Price = Input.Price,
            StockQuantity = Input.StockQuantity,
            Unit = Input.Unit
        };

        if (id == 0)
            catalogAdmin.CreateStockProduct(product);
        else
            catalogAdmin.UpdateStockProduct(product);

        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = "";

        [StringLength(200)]
        public string? NameEn { get; set; }

        [Required, StringLength(500)]
        public string Description { get; set; } = "";

        [StringLength(500)]
        public string? DescriptionEn { get; set; }

        [Required, StringLength(50)]
        public string Sku { get; set; } = "";

        [Required]
        public AccessoryCategory Category { get; set; }

        [Range(0, 100000)]
        public decimal Price { get; set; }

        [Range(0, 100000)]
        public int StockQuantity { get; set; }

        [StringLength(20)]
        public string? Unit { get; set; }
    }
}
