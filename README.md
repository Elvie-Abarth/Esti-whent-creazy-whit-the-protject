# Marsvin — webpage example build

A working ASP.NET Core 9 Razor Pages front end for the guinea pig shop, backed
by a real SQL Server LocalDB database via plain ADO.NET. Built as **visual and
structural inspiration only** — it is deliberately not a solution to the
graded assignment.

> Created with the help of Claude (AI).

## Run it

From the `marsvin-web` folder:

```
dotnet run
```

Open <http://localhost:5080>. Press Ctrl+C to stop.

On first run this automatically creates a `MarsvinDb` database on
`(localdb)\MSSQLLocalDB`, runs the schema, and seeds it with demo data — see
`marsvin-web/DATABASE-NOTES.txt` for where the files live, how to browse the
database, and how to recover if LocalDB gets disconnected.

You need the **.NET 9 SDK**, for VS Code the **C# Dev Kit** extension, and
**SQL Server LocalDB** (ships with Visual Studio, or install separately).
`dotnet --list-sdks` and `sqllocaldb info` tell you what you have.

## Run the tests

From the `marsvin-webpage-example` folder (the solution root):

```
dotnet test
```

52 tests: 41 run entirely in memory (models, page logic, catalog grouping);
11 are integration tests against a disposable `MarsvinDb_Test` LocalDB
database, so they require LocalDB installed and running.

## What's in it

| Page | Route | What it shows |
|---|---|---|
| Front page | `/` | Hero, bonded pairs, the four welfare questions, three essentials |
| Marsvinene | `/Marsvin` | All animals, grouped into the pairs they're sold as |
| Profil | `/Marsvin/Details/{id}` | One animal: breed, sex, age, colour, status, partner |
| Tilbehør | `/Tilbehor` | Accessories with category filtering via query string |

Every page also has an **EN / DA** toggle in the header — the site defaults
to Danish and switches to English client-side, remembering the choice per
browser.

```
Models/          Product (abstract) → StockProduct, Animal
Data/            ICatalog, DemoCatalog (in-memory), SqlCatalog (ADO.NET),
                 DbInitializer, Sql/schema.sql
Pages/           Razor Pages + PageModels
Pages/Shared/    _Layout.cshtml, _Cavy.cshtml (the drawn guinea pig)
wwwroot/css/     site.css — all the design tokens live at the top
wwwroot/js/      lang-toggle.js — the DA/EN switch
marsvin-web.Tests/  xUnit tests for models, pages, and the SQL layer
```

## What is deliberately missing

**No cart, no checkout, no login, no admin.** Those are your assignment —
user stories 1 to 11 — and you learn nothing from me handing them over. This
example stops at the point where your own work starts.

The database layer (`SqlCatalog`, `DbInitializer`, `schema.sql`) uses plain
ADO.NET with parameterised `SqlCommand` — no ORM — matching what the real
assignment expects. The `Product` / `StockProduct` / `Animal` hierarchy is
shaped for the `ProductType` discriminator column, so the models port across
unchanged.

Buttons that would need write operations (reservations, checkout) are marked
`aria-disabled` and say so.

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
imagery. Set an animal's `PhotoUrl` to swap in a real photo — the illustration
stays as the fallback for every animal that doesn't have one yet.

Responsive down to mobile, visible keyboard focus, and `prefers-reduced-motion`
respected.
