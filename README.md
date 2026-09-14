# Marsvin — webpage example build

A working ASP.NET Core 9 Razor Pages guinea pig shop, backed by a real SQL
Server LocalDB database via plain ADO.NET — with accounts, roles, a cart and
checkout, and an admin/employee panel.

> Created with the help of Claude (AI).

**A note on scope.** This repo started as visual/structural inspiration only,
deliberately stopping short of the graded assignment (no cart, no login, no
admin — "you learn nothing from me handing them over"). The auth, cart, and
admin panel described below were added later, at explicit request, after
being told directly that this is most of the graded work for a *secure
coding* course. If this is your assignment: the point of that course is very
likely the security decisions in here (password hashing, session handling,
role-based authorization, preventing IDOR) — copying this in defeats that.
Read it to see one way it can be done, then build your own.

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

108 tests, three layers:

- **41 in-memory unit tests** — models, page logic, catalog grouping. No database needed.
- **61 integration tests against a disposable `MarsvinDb_Test` LocalDB database** — the SQL data layer (`SqlUserAccountStore`, `SqlCartStore`, `SqlOrderStore`, `SqlPromotionStore`, `SqlCatalog`), plus security-critical `PageModel` logic called directly: the checkout transaction (stock validation, rollback on failure), the IDOR guard on order confirmation, the admin self-protection guard, cart validation, and login lockout.
- **6 end-to-end HTTP tests against a disposable `MarsvinDb_WebTest` database**, driving the real app in-process via `WebApplicationFactory<Program>` — the layer above can't see actual antiforgery/CSRF behavior or cookie flags, since calling a `PageModel` method directly skips the middleware pipeline entirely. These prove: a POST without an antiforgery token is rejected (400), a token from a different session is rejected (400) even though it's validly-formed, the auth cookie is `HttpOnly`, and logging out actually expires that cookie and blocks a subsequent request to a protected page.

All three require LocalDB installed and running (`sqllocaldb info`).

## What's in it

| Page | Route | Who | What it shows |
|---|---|---|---|
| Front page | `/` | Everyone | Hero, bonded pairs, the four welfare questions, three essentials |
| Marsvinene | `/Marsvin` | Everyone | All animals, grouped into the pairs they're sold as |
| Profil | `/Marsvin/Details/{id}` | Everyone | One animal: breed, sex, age, colour, status, partner, buy button |
| Tilbehør | `/Tilbehor` | Everyone | Accessories, category filter, stock shown as Available/Low/Out (never a raw number), buy button |
| Log ind / Opret konto | `/Account/Login`, `/Account/Register` | Everyone | Sign in, or self-register a Customer account |
| Kurv | `/Cart/Index` | Customer | View cart, remove lines, check out |
| Kvittering | `/Cart/Confirmation/{orderId}` | Customer | Order summary — only the buyer can view their own order |
| Personale | `/Admin/Index` | Employee, Admin | Dashboard with role-appropriate links |
| Lager & status | `/Admin/Stock/Index` | Employee, Admin | Adjust accessory stock counts, change animal status |
| Tilbehør (admin) | `/Admin/Products/Index` + `Edit` | Admin | Full CRUD on accessories |
| Marsvin (admin) | `/Admin/Animals/Index` + `Edit` | Admin | Full CRUD on guinea pigs |
| Kampagner | `/Admin/Promotions/Index` + `Edit` | Admin | Time-boxed discounts, shop-wide or per product |
| Konti | `/Admin/Users/Index` | Admin | Change roles, activate/deactivate accounts, create Employee/Admin accounts |

Every page also has an **EN / DA** toggle in the header — the site defaults
to Danish and switches to English client-side, remembering the choice per
browser.

Demo login credentials (seeded once, on a fresh database — see
`marsvin-web/DATABASE-NOTES.txt`):

```
Admin:    admin@marsvin.dk     / Admin123!
Employee: employee@marsvin.dk  / Employee123!
Employee: employee2@marsvin.dk / Employee123!
Employee: employee3@marsvin.dk / Employee123!
Employee: employee4@marsvin.dk / Employee123!
```

Customers always self-register; there's no seeded customer account.

```
Models/          Product → StockProduct, Animal; ApplicationUser, CartLine, Order, Promotion
Data/            ICatalog / ICatalogAdmin (catalog), IUserAccountStore, ICartStore,
                 IOrderStore, IPromotionStore — each with a Sql* ADO.NET implementation
                 DbInitializer, Sql/schema.sql
Pages/Account/   Register, Login, Logout, AccessDenied
Pages/Cart/      Index (view/add/remove/checkout), Confirmation
Pages/Admin/     Index, Stock/, Products/, Animals/, Promotions/, Users/
Pages/Shared/    _Layout.cshtml, _Cavy.cshtml (the drawn guinea pig)
wwwroot/css/     site.css — all the design tokens live at the top
wwwroot/js/      lang-toggle.js — the DA/EN switch
marsvin-web.Tests/  xUnit tests for models, pages, and the SQL layer
```

## How the security-relevant parts work

- **Passwords**: hashed with ASP.NET Core's `PasswordHasher<T>` (PBKDF2, salted) — never stored or logged in plain text.
- **Sessions**: cookie authentication (`HttpOnly`, `SameSite=Lax`), issued on login/register, checked server-side on every request via `[Authorize]`.
- **Authorization is server-side, not just hidden buttons.** Every role check happens in the `PageModel` (`[Authorize(Roles = "...")]`); an Employee who navigates straight to an Admin-only URL gets redirected to `/Account/AccessDenied`, not just a missing link in the nav. Verified directly (see git history / test the pages yourself): Employee → 302 on `/Admin/Products`, Customer → 302 on `/Admin/Index`.
- **IDOR guard on orders**: `/Cart/Confirmation/{orderId}` looks up the order *and* checks it belongs to the logged-in user in the same query (`IOrderStore.FindForUser`) — you can't view someone else's order by guessing the ID.
- **Checkout re-validates inside a transaction.** The cart can go stale between "add to cart" and "check out" (someone else buys the last unit, an animal gets marked not-for-sale); `SqlOrderStore.Checkout` re-checks stock/availability against the database inside the same transaction that records the order and decrements stock, and rolls back the whole checkout rather than partially fulfilling it.
- **Self-protection on `/Admin/Users`**: an admin can't change their own role or deactivate their own account (only someone else's), so you can't accidentally lock yourself out.
- **Login throttling**: 5 failed attempts locks that email out for 5 minutes (in-memory — fine for a demo, would need to be persisted for a real multi-instance deployment).
- **No raw SQL string-building anywhere** — every query is a parameterised `SqlCommand`, including the admin CRUD forms.
- **CSRF**: Razor Pages validates the antiforgery token on every POST handler automatically; every form here uses `method="post"` with the framework's tag helpers, which include the token for free.

The database layer (`SqlCatalog`, `SqlUserAccountStore`, `SqlCartStore`,
`SqlOrderStore`, `SqlPromotionStore`) uses plain ADO.NET with parameterised
`SqlCommand` throughout — no ORM. The `Product` / `StockProduct` / `Animal`
hierarchy is shaped for the `ProductType` discriminator column.

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
