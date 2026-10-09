# Marsvin database architecture

This document explains **how the database is built and how it hangs together** -
the schema, the tables, why they're shaped the way they are, and how the
application code talks to them. For operational how-tos (starting the project,
resetting the database, login credentials), see `DATABASE-NOTES.txt` instead -
this document is about the design, not the day-to-day commands.

## 1. The big picture

Marsvin uses **SQL Server LocalDB** - a real SQL Server engine, not a toy
in-memory database, but one that runs privately under your Windows user
account instead of as a shared server. There is **no ORM** (no Entity
Framework, no Dapper) - every single query in this project is hand-written
SQL executed through raw ADO.NET (`Microsoft.Data.SqlClient`). That's a
deliberate constraint of the assignment this project is for, not an
oversight, and it shapes everything else described below: there's no
`DbContext`, no LINQ-to-SQL, no automatic migrations - just SQL strings,
parameters, and manual `SqlConnection`/`SqlCommand` objects.

The chain from a browser click to a database row looks like this:

```
Browser
  -> Razor Page (.cshtml) - the view
  -> PageModel (.cshtml.cs) - the handler (OnGet/OnPost...)
  -> an IXxxStore interface (e.g. IUserAccountStore) - injected via DI
  -> a SqlXxxStore class (e.g. SqlUserAccountStore) - the only place
     that actually contains SQL for that concern
  -> Microsoft.Data.SqlClient (SqlConnection/SqlCommand)
  -> SQL Server LocalDB ((localdb)\MSSQLLocalDB), database MarsvinDb
```

Every `SqlXxxStore` class follows the same shape: a constructor that takes a
connection string, a private `Open()` helper that creates and opens a fresh
`SqlConnection`, and one method per operation that opens a connection, runs
one parameterised `SqlCommand`, and disposes everything via `using`. There is
no connection pooling code of our own - ADO.NET/SQL Server already pool
connections internally by connection string, so opening a new `SqlConnection`
per call is cheap and is what the whole codebase does throughout.

## 2. Schema-as-code: `Data/Sql/schema.sql`

There's no migrations framework (no EF Migrations, no Flyway). Instead,
**the entire schema lives in one plain SQL script**,
`Data/Sql/schema.sql`, and it is re-run in full on *every single app
startup* (see `DbInitializer.RunSchemaScript`). See
`DATABASE-CREATION-SCRIPT.md` for the full script inline and the equivalent
`sqlcmd` commands to build the database by hand, outside the app. That only
works safely because every statement in it is written to be a no-op once
it's already been applied:

- **New tables** are wrapped in `IF OBJECT_ID('dbo.TableName', 'U') IS NULL
  BEGIN ... END` - created the first time the table is missing, skipped
  every time after.
- **New columns on an existing table** are guarded with
  `IF COL_LENGTH('dbo.TableName', 'ColumnName') IS NULL BEGIN ALTER TABLE ...
  ADD ... END` - this is how the schema "evolves" over time without a real
  migration history: a table that already existed before a feature was added
  won't have the new column from its `CREATE TABLE` block, so there's a
  matching `ALTER TABLE ... ADD` guarded the same way, right below it.
- A couple of statements (like widening `Orders.UserId` to nullable) are
  naturally idempotent SQL on their own - re-running `ALTER TABLE ... ALTER
  COLUMN ... NULL` on an already-nullable column is harmless, so those don't
  need an `IF` guard at all. The two `UPDATE dbo.Animals SET PhotoUrl ...`
  lines at the very end are the same kind: they repoint two photos that
  changed file type (`.png` to `.jpg`) and match nothing once that's done.
- **`CHECK` constraints and indexes added to an existing table** follow the
  same guard shape as a new column, just checking `sys.check_constraints` /
  `sys.indexes` instead of `COL_LENGTH`: `IF NOT EXISTS (SELECT 1 FROM
  sys.check_constraints WHERE name = '...') BEGIN ALTER TABLE ... WITH CHECK
  ADD CONSTRAINT ... END`. `WITH CHECK` matters here - it validates the
  constraint against whatever rows already exist at the moment it's added,
  rather than only enforcing it going forward.

**Nothing in this script ever drops or recreates a table.** That used to be
true only for the accounts/orders tables; once admin/employee pages got the
ability to edit the product catalog live, wiping and reseeding
Products/Animals/StockProducts on every `dotnet run` would have erased that
work too. So the rule is now universal: every table is created once, left
alone forever after, and evolved only through additive, guarded `ALTER`
statements.

## 3. The tables

### Catalog: `Products` / `Animals` / `StockProducts`

This is a **table-per-hierarchy-ish** design mirroring the `Product` /
`Animal` / `StockProduct` class hierarchy in C#:

- **`Products`** is the shared base table - every sellable thing (a guinea
  pig or a bag of hay) has exactly one row here, holding the fields every
  product has in common: `ProductId` (a plain `INT` primary key, **not**
  `IDENTITY` - IDs are assigned by the seed data / admin-create code, not
  auto-generated), `ProductType` (`1` = Animal, `2` = StockProduct - the
  discriminator), bilingual `Name`/`NameEn` and `Description`/`DescriptionEn`,
  and `Price`.
- **`Animals`** holds only what's specific to a guinea pig: breed, sex,
  date of birth, colour/coat, availability `Status` (`0` Available, `1`
  Reserved, `2` Sold, `3` NotForSale), an optional `BondedWithId` pointing at
  another animal's `ProductId` (guinea pigs are herd animals - this is how a
  bonded pair is represented), personality text, and a photo. Its primary
  key **is** `ProductId`, with a real foreign key back to `Products` - an
  `Animals` row without a matching `Products` row would be meaningless, and
  the two are always created/deleted together in one place
  (`SqlCatalog.CreateAnimal`/`DeleteAnimal`), so a strict FK is safe here.
- **`StockProducts`** is the accessory-specific counterpart: SKU, category
  (`0` Hay, `1` Food, `2` Cage, `3` House, `4` Toy, `5` Bedding, `6` Care),
  `StockQuantity`, unit, photo, and two columns added later:
  - **`WeightGrams`** - the packed weight of one unit (default 500). This
    is what shipping is priced from: `ShippingCalculator` adds up the
    weights of the shippable lines, splits them into parcels of at most
    20 kg and prices each parcel by carrier. The weight is read from this
    table at checkout, never from the form.
  - **`Brand`** - who makes it (nullable: an unbranded item is fine). It
    drives the brand filter on `/Tilbehor` and the `/Maerker` page. There
    is deliberately no separate `Brands` table: a brand here is just a
    label on a product, with no data of its own to keep consistent.

  Same FK relationship to `Products` as `Animals`, for the same reason.

  Customers never see `StockQuantity` itself - pages show "in stock", "low
  stock" or "sold out" (`StockProduct.StockLevel`), and the support chat is
  only given that wording too.

`BondedWithId` (on `Animals`) and every `ProductId` column on the tables
below are **deliberately not foreign keys**, even though they point at a
`Products` row. The reasoning, straight from the schema's own comments: those
columns reference a product that an admin might legitimately delete later
(discontinue an accessory, remove an animal listing), and a strict FK would
either block that deletion or force a cascade that silently destroys
historical data. So those references are checked in application code
instead (e.g. `OrderItems` keeps the product's name as a **snapshot** at
time of purchase - see below - rather than needing the product to still
exist at all).

### Accounts: `Users`

One row per account, whether Customer, Employee, or Admin (`Role`: `0`/`1`/`2`):
`Email` (unique), `PasswordHash` (PBKDF2 via ASP.NET Core's
`PasswordHasher<T>` - never plain text), `DisplayName`, `IsActive`,
`CreatedAt`, `TotpSecret`/`TotpEnabled` for the optional second factor
(the secret is encrypted at rest), and:

- **`SecurityStamp`** - a random `UNIQUEIDENTIFIER` (`DEFAULT NEWID()`, so
  every account, old or new, has its own). It is copied into the auth
  cookie at sign-in and compared on every request; `UpdatePassword`
  replaces it in the same `UPDATE` that stores the new hash, and "log out
  everywhere" replaces it on its own (`RotateSecurityStamp`). A cookie
  carrying an older stamp is rejected on its next request - which is what
  makes changing a password actually end a session someone else may be
  holding. It is not a secret (it only ever travels inside the encrypted
  cookie); it just has to be different each time.

Two more columns exist specifically to drive the GDPR 2-year inactivity
policy:

- **`LastActiveAt`** - set at registration, refreshed every time a login is
  *fully confirmed* (see the `PendingLogins` section below - not just when a
  password is typed correctly).
- **`InactivityWarningStage`** - `0` (nothing sent yet), `1` ("2 months
  left" warning already emailed), or `2` ("1 month left" warning already
  emailed). This exists purely so the daily cleanup job (see §5) doesn't
  re-send the same warning every single day while an account sits in the
  warning window - and it's reset to `0` on every confirmed login, so a
  returning customer's countdown genuinely restarts rather than carrying a
  stale "already warned" flag toward some future inactivity period.

### `PendingLogins` - the email login-confirmation step

A correct password alone doesn't create a session anymore. `LoginModel`
verifies the password, then writes a row here instead of signing anyone in:
a `UserId`, a `TokenHash`, an optional `ReturnUrl` to redirect back to once
confirmed, and an `ExpiresAt` (15 minutes out). The **raw token is only ever
in the email** sent to the account's address - the database stores just its
SHA-256 hash (`TokenHash`), the same principle as never storing a password in
plain text: if this table leaked, nobody could reconstruct a working login
link from it. `ConfirmLoginModel` hashes whatever token comes back in the
URL, looks for a matching, unexpired row, and if found, deletes it (single
use) and *then* creates the real signed-in session.

There is a real FK from `PendingLogins.UserId` to `Users`, and at most one
row per user at a time - a fresh login attempt deletes any earlier
unconfirmed one first, so this table can never grow unbounded no matter how
many times someone retries a login.

### `CartItems`

One row per (user, product) line in a customer's in-progress cart - a
`UNIQUE (UserId, ProductId)` constraint is what makes "add to cart" an
upsert (increment quantity if the line already exists) rather than ever
creating duplicate lines for the same product. Has a real FK to `Users`
(a cart with no owner makes no sense), but `ProductId` is a plain column for
the reason explained above.

### `Orders` / `OrderItems`

`Orders` is the header row (who bought it, total price, when);
`OrderItems` is one row per line item within that order. The interesting
design choice is on `Orders.UserId`: it **started out `NOT NULL`** and was
deliberately loosened to `NULL` later, specifically so that deleting a
customer's account (self-service, by an admin, or by the automatic
inactivity job) never has to choose between "block the deletion" and
"destroy the sales history." Deleting an account sets `Orders.UserId = NULL`
on their past orders instead of deleting the rows - the order survives as an
anonymous historical record, it just isn't "for" anyone anymore.

`OrderItems.ProductId` is (again) not a FK, and `OrderItems` also stores its
own `ProductName` and `UnitPrice` columns - a **snapshot** of what the
product was called and cost *at the moment of purchase*. If an admin later
renames or deletes that product, an old receipt still reads correctly
instead of showing a broken reference or today's (possibly different) price.

Several more columns were added for accessory shipping, demo payment,
guest checkout, order status and company purchases. `Orders` has `DeliveryMethod` (`0` Pickup, `1`
Shipping) and `ShippingAddress` (only ever set when shipping), plus
`ShippingCarrier` (`0` PostNord, `1` GLS, `2` DAO Pakkeshop - also only set
when shipping; a `NULL` carrier on an otherwise-valid Shipping checkout
defaults to PostNord rather than failing, since every carrier radio on the
payment form is pre-selected) and `PaymentMethod` (`0` Card, `1` MobilePay
- both demo-only, no real processing either way). Shipping ships whatever's
shippable in the order - a guinea pig always still needs a separate
in-store pickup no matter what's chosen, so `OrderItems` also has its own
`IsAnimal` snapshot column (same idea as `ProductName`/`UnitPrice`): it's
what lets the order confirmation page name exactly which item(s) in a
shipped order still need picking up, without needing to re-join back to
`Animals` (whose row may itself later be edited or deleted) to find out.
`SqlOrderStore.Checkout` only rejects `Shipping` outright when the order
is *entirely* animals - a mixed cart ships the accessory half and still
sells the animal in the same transaction.

`GuestName`/`GuestEmail` are the guest-checkout counterpart to `UserId`
being `NULL` - unlike a deleted account's order (`UserId` also `NULL`,
but it *used to* point at a real Users row), a guest checkout never had
one to begin with, so there's no name/email to join back to at all; these
two columns are what the admin order list, the confirmation email, and
the receipt itself fall back to instead. A guest's cart was never in
`CartItems` either - see `SessionCartStore` below.

**What the shipment cost and weighed** is stored on the order, not worked
out again later: `ShippingCost` (already included in `TotalPrice`; `0` for
pickup, and `0` when the shipped accessories reach the free-shipping
threshold of 499 kr.), `ShippingWeightGrams` and `ParcelCount` (both `NULL`
for pickup). They are snapshots for the same reason `UnitPrice` is: if the
rates or a product's weight change next month, an old receipt must still
say what was actually charged. All three are calculated by
`SqlOrderStore.Checkout` itself, inside the checkout transaction - the
payment form shows the same numbers, but nothing it posts is trusted for
them. The *expected delivery window* is not stored at all: it is derived
from `CreatedAt` and the carrier whenever it is shown.

**`Status`** follows the order after checkout: `0` Placed, `1` Processing,
`2` Sent (or "ready for pickup" - the wording follows `DeliveryMethod`),
`3` Completed (delivered / picked up), `4` Cancelled. Staff move it along
on `/Admin/Orders`, optionally with a **`TrackingNumber`** from the
carrier; each change is audit-logged and emailed to the buyer.
`Cancelled` is deliberately the highest number: "may still be cancelled"
is then a plain `Status <= @latest` comparison (the buyer may cancel while
it is still `Placed`), and an already cancelled order can never match it. A cancelled order is final -
`UpdateStatus` refuses to move one (`WHERE ... AND Status <> Cancelled`),
because its items have already gone back into stock.

**`ContactPhone`** is the number the carrier texts (required for shipping,
optional for pickup). It is stored with the order only, never on the
account. **`AgeConfirmed`** records that the buyer ticked "I am 16 or
older" for an order containing a guinea pig - a yes/no on purpose, not an
age or a date of birth: the shop needs to know the rule was met, not how
old anyone is (data minimisation).

**`CompanyName` / `CompanyCvr`** are set together, or not at all, for a
company, school or institution purchase. `CompanyCvr` is `CHAR(8)`: the
form accepts spaces, but only the eight digits are stored, after the
CVR check-digit rule has been verified in code (`Cvr.IsValid`). The VAT
amount on such a receipt is calculated from `TotalPrice` when shown, not
stored.

### `Promotions`

Time-boxed discounts (`StartDate`/`EndDate`, `DiscountPercent`), optionally
scoped to one product via a non-FK `ProductId` (same reasoning as above - a
promotion can outlive the product it was created for without becoming
invalid data).

### `Shifts` - the work schedule

One row per scheduled shift: which staff member (`UserId`, a **real** FK to
`Users` this time - a shift genuinely doesn't mean anything once the
account it belongs to is gone, unlike an order), `StartAt`/`EndAt`
(`DATETIME2`, so date and time together in one column each), and an optional
`Note`. An Admin creates and deletes these from `/Admin/Schedule`; an
Employee can only ever read their own.

### `TimeOffRequests` - day-off requests

An Employee's own request to be off work for a date range, with a
`Status` (`0` Pending, `1` Approved, `2` Denied), the free-text `Reason`
they gave, and - once an Admin has acted on it - `DecidedAt` and
`DecidedByName`. `DecidedByName` is a **snapshot** of the deciding admin's
display name, not a foreign key to their account, for exactly the same
reason `OrderItems.ProductName` is a snapshot: the record of *who approved
this* should survive even if that admin's own account is deleted later.
`UserId` (the requester) is a real FK, same reasoning as `Shifts`.

### `ContactMessages` - the public contact form

One row per message sent from `/Kontakt`: `Name`, `Email`, a `Topic`
(`0` Order, `1` GuineaPig, `2` Accessory, `3` Company, `4` Other), the
`Message` (up to 2000 characters) and `CreatedAt`. Staff read them on
`/Admin/Messages`.

`OrderId` here is **whatever number the sender typed**, which is why it is
a plain nullable column and not a foreign key: it is not proof the order
exists, and certainly not proof it belongs to the sender. Nothing is looked
up or shown back from it - it is only a hint for the staff member reading
the message. There is no `UserId` either: the form is public, and a message
is not tied to an account even when the sender happens to be signed in.

### `Donations` - support for rehomed guinea pigs

One row per donation from `/Stoet`, read on `/Admin/Donations`. `Kind` is
`0` Money or `1` Products, and decides which of the other columns are
used: `AmountKr` only for money, `ItemDescription` only for products.
`DonorName`, `DonorEmail` and `Message` are all optional - an anonymous
money donation is fine. Like the checkout, a money donation is a demo: it
is recorded, never charged, and no card details are asked for or stored.

### `SeedBatches` - one-off additions to the demo catalog

A two-column bookkeeping table (`Name`, `AppliedAt`) with one row per
batch of demo data that was added to the catalog *after* databases already
existed. The catalog is only seeded into an empty database (§6), so when
it later grew from 26 to 71 accessories, existing databases needed the new
ones exactly once. A row here means "done - do not add them again", which
is what keeps a product an admin has since deleted from reappearing on the
next restart. It holds no shop data and no page reads it.

### `AuditLog` - who changed what, and when

Every admin/employee write action worth being able to answer "who did this"
about later - a price or stock change, a role change, a deletion, a staff
account being created - writes one row here: `ActorUserId` (not a FK, for
the same reason as `OrderItems.ProductName` - the entry has to survive that
admin's own account later being deleted), a snapshot `ActorName`, a short
machine-readable `Action` (e.g. `"Product.StockChanged"`), a human-readable
`Details` string, and `CreatedAt`. Nothing in the application ever updates
or deletes a row here once written - it's the accountability record, read
from `/Admin/AuditLog` and never edited.

## 4. How the tables relate to each other

```
Users ──┬──< CartItems
        ├──< Orders (UserId nullable - survives account deletion)
        │       └──< OrderItems (ProductId not FK - snapshot instead)
        ├──< PendingLogins (single-use email-confirmation tokens)
        ├──< Shifts (work schedule)
        └──< TimeOffRequests (DecidedByName is a snapshot, not a FK)

Products ──┬──< Animals (ProductId is both PK and FK)
           └──< StockProducts (ProductId is both PK and FK)

AuditLog - standalone (ActorUserId not a FK - a snapshot record, like
           OrderItems.ProductName, that outlives the acting account)

ContactMessages - standalone (OrderId is what the sender typed, not a FK)
Donations       - standalone (no account, no order)
SeedBatches     - standalone (startup bookkeeping only)

(CartItems.ProductId, OrderItems.ProductId, Promotions.ProductId,
 Animals.BondedWithId - all plain INT columns pointing at Products.ProductId,
 deliberately NOT foreign keys, so a product can be deleted by an admin
 without being blocked by, or silently deleting, unrelated historical data)
```

## 5. How the app actually reads and writes this data

### The store pattern

Every table (or small cluster of related tables) has exactly one interface
and one SQL implementation in `Data/`:

| Interface | Implementation | Covers |
|---|---|---|
| `ICatalog` / `ICatalogAdmin` | `SqlCatalog` | Products, Animals, StockProducts |
| `IUserAccountStore` | `SqlUserAccountStore` | Users |
| `IPendingLoginStore` | `SqlPendingLoginStore` | PendingLogins |
| `ICartStore` | `SqlCartStore` (signed in) / `SessionCartStore` (guest) | CartItems, or nothing at all - a guest's cart lives in session instead, see §4 of WEBSITE-ARCHITECTURE.md |
| `IOrderStore` | `SqlOrderStore` | Orders, OrderItems |
| `IPromotionStore` | `SqlPromotionStore` | Promotions |
| `IShiftStore` | `SqlShiftStore` | Shifts |
| `ITimeOffRequestStore` | `SqlTimeOffRequestStore` | TimeOffRequests |
| `IAuditLogStore` | `SqlAuditLogStore` | AuditLog |
| `IInboxStore` | `SqlInboxStore` | ContactMessages, Donations |

`SeedBatches` has no store: only `DbInitializer` touches it, at startup.

The support chat ("Pip") has no table at all. It answers from the catalog
through the same `ICatalog` as the pages do, plus texts compiled into the
app (`Data/ShopKnowledge.cs`), and nothing a visitor types is written to
the database - there is no chat history to protect, leak or delete.

Pages depend on the **interface**, never the concrete `SqlXxx` class
directly - that's what lets tests substitute the real SQL implementation
against a throwaway test database (see `marsvin-web.Tests`) without any
mocking framework: the tests use the exact same `SqlXxxStore` classes
against a dedicated `MarsvinDb_Test` database that's created fresh and
dropped again per test run, so "the test passed" really does mean "this SQL
round-tripped correctly through a real SQL Server," not just "a mock
returned what I told it to."

`Program.cs` wires all of this up as `Scoped` (one instance, and one
underlying connection, per web request) - except `IEmailSender`, which is
`Singleton` (see §7), and the DB connection string itself, which is read
once from `appsettings.json`'s `ConnectionStrings:MarsvinDb` and closed over
by every store's registration lambda.

### Every query is parameterised

There is no string concatenation of user input into SQL anywhere in this
project - every value from a form, a query string, or a route goes in as a
`SqlParameter` (`command.Parameters.AddWithValue(...)`), never interpolated
directly into the SQL text. That's the entire SQL-injection defence, and
it's structural rather than something that has to be remembered per query:
the pattern is the same in all ten `SqlXxxStore` classes.

### Transactions, where it actually matters

Most single-table writes don't need an explicit transaction - one
`SqlCommand` is already atomic. A few operations touch more than one table
and use `connection.BeginTransaction()` so they succeed or fail together:

- **`SqlUserAccountStore.DeleteUser`** - deletes the account's `CartItems`,
  `Shifts`, and `TimeOffRequests` (all three have real FKs to `Users`, so
  they have to go first), orphans their `Orders` (`UserId = NULL`, keeping
  the rows), and only then deletes the `Users` row itself - all in one
  transaction, so a failure partway through can't leave a half-deleted
  account.
- **`SqlPendingLoginStore.Create`/`Consume`** - `Create` deletes any earlier
  pending token for that user and inserts the new one atomically; `Consume`
  reads the matching row and deletes it (single-use) in one transaction, so
  two near-simultaneous confirmation attempts can't both succeed. (There's
  also `IsValid`, a non-transactional read-only peek used by
  `Account/ResetPassword`'s `OnGet` to tell a visitor a link is dead up
  front, without spending the token before they've even filled in the form -
  the actual redemption still only ever happens through `Consume`.)
- **`SqlOrderStore.Checkout`** - re-validates stock/availability *inside* the
  same transaction that inserts the order and decrements stock, using
  `SELECT ... WITH (UPDLOCK, HOLDLOCK)` on the row being checked rather than
  a plain `SELECT`. A plain read releases its lock immediately, so two
  checkouts racing for the last unit of something (or the same guinea pig)
  could both read "still available" under READ COMMITTED and both succeed -
  the row lock makes the second checkout block until the first commits or
  rolls back, then re-read the now-updated row instead of the stale one.
  The shipping cost, weight and parcel count are calculated in that same
  transaction, from the weights it has just read.
- **`SqlOrderStore.Cancel`** - the mirror image of checkout. In one
  transaction, a single `UPDATE ... SET Status = Cancelled WHERE OrderId =
  @id AND Status <= @latest` both checks that it is still early enough to
  cancel and makes the change - there is no separate read for a second
  request to slip in after. Only if that changed a row does it go on to
  put every accessory line's quantity back into `StockProducts` and set
  each animal in the order from `Sold` back to `Available`. Either all of
  that happens or none of it: an order can't end up cancelled with its
  stock still missing, and a second click finds nothing left to cancel, so
  nothing is restocked twice.
- **`SqlOrderStore.ClaimGuestOrder`** - moves a guest order into an account
  (`UserId` set, guest name/email cleared) only where the order's
  `GuestEmail` matches the account's email, in a single `UPDATE` whose
  `WHERE` clause carries both conditions - so the check and the change
  can't be separated.

## 6. Startup: how the database comes into existence

`DbInitializer.EnsureCreatedAndSeeded` runs once, synchronously, right at
the top of `Program.cs`, before the web server starts accepting requests:

1. **`EnsureDatabaseExists`** - connects to the `master` database and runs
   `CREATE DATABASE MarsvinDb` if it doesn't exist yet.
2. **`RunSchemaScript`** - reads `Data/Sql/schema.sql` off disk and executes
   it as one batch (see §2 - safe to run every time).
   Just before and after it, **`BackfillDemoWeights`** and
   **`BackfillDemoBrands`** handle one special case: the single startup on
   which `StockProducts.WeightGrams` or `Brand` is added to a database that
   already has products. Every existing row would get the column default
   (500 g, no brand), which is wrong for a 16 kg cage - so the demo
   catalog's own values are written in once, matched by `Sku`. Whether to
   do it is decided *before* the schema script runs (is the column missing
   from an existing table?), which is what makes it once-only: a weight an
   admin edits later is never overwritten.
3. **`SeedIfEmpty`** - if `Products` has zero rows (i.e. this is a genuinely
   fresh database), seeds the whole catalog from the in-memory
   `DemoCatalog` class. If the catalog already has rows - which it will on
   every run after the first, including after an admin has added, edited,
   or removed products - this is skipped entirely, so admin edits are never
   overwritten by a restart.
4. **`AddSecondAccessoryBatchOnce`** - adds the accessories that joined the
   demo catalog later (`ProductId` 127 and up) to a database that was
   seeded before they existed. Each is matched by `Sku` and skipped if it
   is already there; if an admin-created product happens to have taken its
   `ProductId`, the next free one is used instead. The batch is then
   recorded in `SeedBatches` and never runs again. On a brand-new database
   step 3 has already inserted everything, so this only writes the marker.
5. **`SeedAccountsIfEmpty`** - same idea for `Users`: only on a genuinely
   empty table does it create the demo Admin and Employee accounts (see
   `DATABASE-NOTES.txt` for the full credentials list - several Employee
   accounts are seeded, not just one, so the "assign a shift" staff picker
   on `/Admin/Schedule` has more than one real choice to demonstrate).
   Customers always self-register; there's no seeded customer account.

Because every step here is either idempotent SQL, guarded by a row-count
check, or recorded as done, `dotnet run` is safe to run any number of times against the same
database - it never resets anything that already has real data in it.

## 7. Email is not part of the SQL Server story, but it's wired through it

Several features (the login/register/reset-password confirmation links, the
inactivity-warning / day-off-request / order-confirmation / order-status
notifications) read
data out of these tables and then send real email via SMTP (`SmtpEmailSender`,
using MailKit and Gmail). The SMTP credentials themselves are **not** stored
anywhere in this database or in any file that gets committed -
`Email:Username`/`Email:Password` are set locally with `dotnet user-secrets`,
kept entirely outside both the repo and `MarsvinDb`. If `Email:Username`
isn't set, `Program.cs` registers `LoggingEmailSender` instead - it just logs
the message rather than sending it, so the app (including the
email-confirmation login flow) is still fully usable without any SMTP setup.
`IEmailSender` is registered `Singleton` (unlike the per-request `Scoped`
stores) because it holds no per-request state - it's just a mail client
wrapper, safe to share across the whole app's lifetime.

The one background piece that ties directly back into this schema is
**`InactiveAccountCleanupService`** (`Data/InactiveAccountCleanupService.cs`),
a `BackgroundService` that runs once a day: it deletes Customer accounts
whose `LastActiveAt` is over 2 years old (reusing the same
`DeleteUser`/orphan-orders logic described in §5), then emails a "2 months
left" warning to anyone approaching that cutoff who hasn't been warned yet
(`InactivityWarningStage < 1`), then a "1 month left" warning to anyone
closer still (`InactivityWarningStage < 2`) - updating that column each time
so the same warning is never sent twice.
