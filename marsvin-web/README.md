# Marsvin — webpage example build

A working ASP.NET Core 9 Razor Pages front end for the guinea pig shop.
Built as **visual and structural inspiration only** — it is deliberately not a
solution to the graded assignment.

## Run it

Open the folder in VS Code (`File → Open Folder…`), then in the terminal:

```
dotnet run
```

Open <https://localhost:7080>. Press Ctrl+C to stop.

You need the **.NET 9 SDK** and, for VS Code, the **C# Dev Kit** extension.
`dotnet --list-sdks` tells you what you have.

## What's in it

| Page | Route | What it shows |
|---|---|---|
| Front page | `/` | Hero, bonded pairs, the four welfare questions, three essentials |
| Marsvinene | `/Marsvin` | All animals, grouped into the pairs they're sold as |
| Profil | `/Marsvin/3` | One animal: breed, sex, age, colour, status, partner |
| Tilbehør | `/Tilbehor` | Accessories with category filtering via query string |

```
Models/         Product (abstract) → StockProduct, Animal
Data/           DemoCatalog — hard-coded data, no database
Pages/          Razor Pages + PageModels
Pages/Shared/   _Layout.cshtml, _Cavy.cshtml (the drawn guinea pig)
wwwroot/css/    site.css — all the design tokens live at the top
```

## What is deliberately missing

**No cart, no checkout, no login, no admin, no database.** Those are your
assignment — user stories 1 to 11 — and you learn nothing from me handing them
over. This example stops at the point where your own work starts.

There is also **no ORM**, in line with the assignment. `DemoCatalog` returns
hard-coded lists; in your real project that class becomes an ADO.NET repository
running parameterised `SqlCommand` against SQL Server. The `Product` /
`StockProduct` / `Animal` hierarchy is already shaped for the `ProductType`
discriminator column in the ER diagram, so the models port across unchanged.

Buttons that would need a back end are marked `aria-disabled` and say so.

## Design notes

The palette comes from the shop's own materials rather than a generic template:
dried hay (`#F0E7D2`), meadow green (`#2C4327`), and the bright fleece
(`#B83C6E`) that guinea pig owners actually line cages with.

Two structural decisions worth keeping if you rebuild the styling yourself:

**Animals are bordered, accessories are not.** A guinea pig gets a full border
and its own profile because it is an individual; a bag of hay gets a hairline
rule because it's stock. The visual difference carries the same information as
the class hierarchy.

**Bonded pairs are drawn as one unit** with a shared header strip. The rule that
guinea pigs are sold in pairs is the shop's whole character, so the layout states
it rather than burying it in a paragraph.

Each animal is drawn as inline SVG tinted from its own `CoatPrimary` and
`CoatSecondary` values, so no photographs are needed and nothing looks like stock
imagery. Swap in real photos when you have them.

Responsive down to mobile, visible keyboard focus, and `prefers-reduced-motion`
respected.
