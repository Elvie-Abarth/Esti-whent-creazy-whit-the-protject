using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static MarsvinWebExample.Pages.PageModelExtensions;

namespace MarsvinWebExample.Pages.Admin.Products;

[Authorize(Roles = "Admin")]
public class EditModel(ICatalog catalog, ICatalogAdmin catalogAdmin, IAuditLogStore audit, IWebHostEnvironment environment)
    : PageModel
{
    public int ProductId { get; set; }
    public bool IsNew => ProductId == 0;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? PhotoFile { get; set; }

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
            Unit = item.Unit,
            PhotoUrl = item.PhotoUrl
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ProductId = id;
        if (!ModelState.IsValid) return Page();

        if (PhotoFile is not null && PhotoFile.Length > 0)
        {
            var url = await PhotoUploadHelper.SaveAsync(PhotoFile, "products", environment, ModelState);
            if (url is null) return Page();
            Input.PhotoUrl = url;
        }

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
            Unit = Input.Unit,
            PhotoUrl = Input.PhotoUrl
        };

        if (id == 0)
        {
            catalogAdmin.CreateStockProduct(product);
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Product.Created", product.Name);
        }
        else
        {
            catalogAdmin.UpdateStockProduct(product);
            audit.Record(this.CurrentUserId(), this.CurrentDisplayName(), "Product.Updated", product.Name);
        }

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

        [StringLength(300)]
        public string? PhotoUrl { get; set; }
    }
}
