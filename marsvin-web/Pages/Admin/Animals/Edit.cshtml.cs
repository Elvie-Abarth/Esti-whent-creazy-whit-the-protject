using System.ComponentModel.DataAnnotations;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarsvinWebExample.Pages.Admin.Animals;

[Authorize(Roles = "Admin")]
public class EditModel(ICatalog catalog, ICatalogAdmin catalogAdmin) : PageModel
{
    public int ProductId { get; set; }
    public bool IsNew => ProductId == 0;
    public IReadOnlyList<Animal> OtherAnimals { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet(int id)
    {
        ProductId = id;
        OtherAnimals = catalog.Animals.Where(a => a.ProductId != id).ToList();

        if (id == 0)
        {
            Input.DateOfBirth = DateOnly.FromDateTime(DateTime.Today);
            return Page();
        }

        var animal = catalog.FindAnimal(id);
        if (animal is null) return NotFound();

        Input = new InputModel
        {
            Name = animal.Name,
            Description = animal.Description,
            DescriptionEn = animal.DescriptionEn,
            Breed = animal.Breed,
            BreedEn = animal.BreedEn,
            Sex = animal.Sex,
            DateOfBirth = animal.DateOfBirth,
            Colour = animal.Colour,
            ColourEn = animal.ColourEn,
            CoatPrimary = animal.CoatPrimary,
            CoatSecondary = animal.CoatSecondary,
            Status = animal.Status,
            BondedWithId = animal.BondedWithId,
            Personality = animal.Personality,
            PersonalityEn = animal.PersonalityEn,
            PhotoUrl = animal.PhotoUrl,
            Price = animal.Price
        };
        return Page();
    }

    public IActionResult OnPost(int id)
    {
        ProductId = id;
        OtherAnimals = catalog.Animals.Where(a => a.ProductId != id).ToList();
        if (!ModelState.IsValid) return Page();

        var animal = new Animal
        {
            ProductId = id,
            Name = Input.Name,
            Description = Input.Description,
            DescriptionEn = Input.DescriptionEn,
            Breed = Input.Breed,
            BreedEn = Input.BreedEn,
            Sex = Input.Sex,
            DateOfBirth = Input.DateOfBirth,
            Colour = Input.Colour,
            ColourEn = Input.ColourEn,
            CoatPrimary = Input.CoatPrimary,
            CoatSecondary = Input.CoatSecondary,
            Status = Input.Status,
            BondedWithId = Input.BondedWithId,
            Personality = Input.Personality,
            PersonalityEn = Input.PersonalityEn,
            PhotoUrl = Input.PhotoUrl,
            Price = Input.Price
        };

        if (id == 0)
            catalogAdmin.CreateAnimal(animal);
        else
            catalogAdmin.UpdateAnimal(animal);

        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [Required, StringLength(500)]
        public string Description { get; set; } = "";

        [StringLength(500)]
        public string? DescriptionEn { get; set; }

        [Required, StringLength(100)]
        public string Breed { get; set; } = "";

        [StringLength(100)]
        public string? BreedEn { get; set; }

        [Required]
        public Sex Sex { get; set; }

        [Required]
        public DateOnly DateOfBirth { get; set; }

        [Required, StringLength(100)]
        public string Colour { get; set; } = "";

        [StringLength(100)]
        public string? ColourEn { get; set; }

        [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Skal være en hex-farve, fx #B5643C.")]
        public string CoatPrimary { get; set; } = "#B5643C";

        [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Skal være en hex-farve, fx #F3EAD8.")]
        public string CoatSecondary { get; set; } = "#F3EAD8";

        [Required]
        public AnimalStatus Status { get; set; }

        public int? BondedWithId { get; set; }

        [StringLength(500)]
        public string Personality { get; set; } = "";

        [StringLength(500)]
        public string? PersonalityEn { get; set; }

        [StringLength(300)]
        public string? PhotoUrl { get; set; }

        [Range(0, 100000)]
        public decimal Price { get; set; }
    }
}
