# Marsvin — webpage example build

A working ASP.NET Core 9 Razor Pages webshop for a guinea pig store, backed
by real ADO.NET + SQL Server (no ORM). This folder is the whole app — the
earlier "inspiration-only, no cart/checkout/login/admin/database" starter
scaffold this project began as has since grown into a complete build with
all of that. For the full picture (every route, the security write-up, how
the tests are laid out, the documentation index), see the
[top-level README](../README.md) one folder up; this file is just the
quick-start for working inside `marsvin-web/` itself.

## Run it

Open this folder in VS Code (`File → Open Folder…`), then in the terminal:

```
dotnet run
```

Open <http://localhost:5080>. Press Ctrl+C to stop.

On first run this creates a `MarsvinDb` database on
`(localdb)\MSSQLLocalDB`, runs the schema, and seeds it with demo data — see
`DocumentationInformation/DATABASE-NOTES.txt` for where the files live, how
to browse the database, and how to recover if LocalDB gets disconnected.

You need the **.NET 9 SDK**, for VS Code the **C# Dev Kit** extension, and
**SQL Server LocalDB** (ships with Visual Studio, or install separately).
`dotnet --list-sdks` and `sqllocaldb info` tell you what you have.

## What's in it

| Page | Route | Who | What it shows |
|---|---|---|---|
| Front page | `/` | Everyone | Hero, bonded pairs, the four welfare questions, three essentials |
| Marsvinene | `/Marsvin` | Everyone | All animals, grouped into the pairs they're sold as |
| Profil | `/Marsvin/Details/{id}` | Everyone | One animal: breed, sex, age, colour, status, partner, buy button |
| Tilbehør | `/Tilbehor` | Everyone | Accessories: search, filters (category/price/brand), sorting, stock shown as Available/Low/Out |
| Info pages | `/Faq`, `/Maerker`, `/Kontakt`, `/Stoet`, `/OmOs`, `/BetalingOgLevering`, `/Privatliv`, care guide, food list | Everyone | FAQ, brands, contact form, donations, terms, guides |
| Log ind / Opret konto | `/Account/Login`, `/Account/Register` | Everyone | Sign in, or self-register — both require an emailed confirmation link |
| Kurv / Betaling | `/Cart/Index`, `/Cart/Payment` | Customer or guest | Cart, pickup-or-shipping (+ carrier, priced server-side), demo Card/MobilePay checkout |
| Kvittering | `/Cart/Confirmation/{orderId}` | The buyer | Order receipt with status and cancel, and a confetti animation on load |
| Personale / admin | `/Admin/*` | Employee, Admin | Schedule, stock, orders (status + tracking), messages, donations, catalog/promotions CRUD, accounts, audit log |

The guinea pig in the corner of every page is the support chat ("Pip") - a
keyword lookup inside the app, not an AI service.

(The full route table with every admin sub-page lives in the top-level
README and in `DocumentationInformation/WEBSITE-ARCHITECTURE.md`.)

```
Models/          Product → StockProduct, Animal; ApplicationUser, CartLine, Order,
                 Promotion, Shift, TimeOffRequest, AuditLogEntry, Bilingual
Data/            ICatalog / IUserAccountStore / ICartStore / IOrderStore / ... —
                 each with a Sql* ADO.NET implementation; DbInitializer, Sql/schema.sql
Pages/           Razor Pages + PageModels (Account/, Cart/, Admin/, Marsvin/, Tilbehor/)
Pages/Shared/    _Layout.cshtml, _Cavy.cshtml (the drawn guinea pig)
wwwroot/css/     site.css — all the design tokens live at the top
wwwroot/js/      lang-toggle, confirm-delete, cart-quantity, password-meter, confetti, chat, payment, ...
```

## Design notes

The palette comes from the shop's own materials rather than a generic template:
dried hay (`#F0E7D2`), meadow green (`#2C4327`), and the bright fleece
(`#B83C6E`) that guinea pig owners actually line cages with.

Two structural decisions worth keeping if you rebuild the styling yourself:

**Animals are bordered, accessories are not.** A guinea pig gets a full border
and its own profile because it is an individual; a bag of hay gets a hairline
rule because it's stock. The visual difference carries the same information as
the class hierarchy.

**Bonded pairs are drawn as one unit** with a shared header strip, and adding
one half of a pair to the cart automatically adds the other — the rule that
guinea pigs are sold in pairs is the shop's whole character, so both the
layout and the checkout state it rather than burying it in a paragraph.

Each animal without an uploaded photo is drawn as inline SVG tinted from its
own `CoatPrimary` and `CoatSecondary` values, so a missing photo never looks
like a broken image or generic stock art.

Responsive down to mobile, visible keyboard focus, and `prefers-reduced-motion`
respected.
