# Marsvin — webpage example build

A working ASP.NET Core 9 Razor Pages guinea pig shop, backed by a real SQL
Server LocalDB database via plain ADO.NET — with accounts, roles, a cart and
checkout, staff scheduling, and an admin panel.

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
`marsvin-web/DocumentationInformation/DATABASE-NOTES.txt` for where the files
live, how to browse the database, and how to recover if LocalDB gets
disconnected.

Login here is email-confirmation-only (see below) — without real SMTP
credentials configured, confirmation links are logged to the console instead
of emailed, so the app is fully usable out of the box. To send real mail, set
`dotnet user-secrets set Email:Username ...` / `Email:Password ...` from the
`marsvin-web` folder (see `DocumentationInformation/DATABASE-NOTES.txt`).

Login/Register/ForgotPassword also support Google reCAPTCHA, the same way:
without a `Recaptcha:SiteKey`/`Recaptcha:SecretKey` configured, no widget is
shown and nothing is verified, so those pages work exactly as before. To
turn it on, get a reCAPTCHA v2 ("I'm not a robot" checkbox) key pair from
<https://www.google.com/recaptcha/admin>, put the site key in
`appsettings.json`'s `Recaptcha:SiteKey` (it's public - embedded straight
into the page), and set the secret key with
`dotnet user-secrets set Recaptcha:SecretKey ...` (never commit that one).

The support chat ("Pip", the guinea pig in the corner) needs no setup and
no key: it is a keyword lookup inside the app, not an AI service, so it
costs nothing to run. It knows the shop's public content - animals,
accessories and brands, the care guide and food list, delivery, returns, and
the FAQ (`Data/ShopKnowledge.cs`) - and tolerates misspellings (one or two
wrong letters, or aa/ae/oe typed for the Danish letters). It can also work a
few things out: whether the shop is open right now, what a parcel of a
given weight costs to send, how big a cage a number of guinea pigs needs,
which animals are for sale, and a yes or no on a food. It understands a
follow-up ("what does she cost?") and several questions in one message, and
the "i" button in the chat shows what can be asked. It knows nothing about
people (orders, accounts, customers, staff) and nothing about how the site
is built or secured, and turns such questions away - there is nothing
private in its knowledge for any wording to dig out. It also refuses a
message containing an email address or a long number, and answers anything
that sounds like a sick animal with "contact a vet", never a diagnosis.

Nothing in this project costs money to run: the database is LocalDB, email
is logged to the console unless you add your own SMTP account, reCAPTCHA is
off unless you add keys, and payment and donations are demos.

You need the **.NET 9 SDK**, for VS Code the **C# Dev Kit** extension, and
**SQL Server LocalDB** (ships with Visual Studio, or install separately).
`dotnet --list-sdks` and `sqllocaldb info` tell you what you have.

## Run the tests

From the `marsvin-webpage-example` folder (the solution root):

```
dotnet test
```

566 tests, three layers:

- **In-memory unit tests** — models, bilingual text handling, page logic that
  needs no database (e.g. cart-line totals, catalog grouping).
- **Integration tests against a disposable `MarsvinDb_Test` LocalDB
  database** — the SQL data layer (`SqlUserAccountStore`, `SqlCartStore`,
  `SqlOrderStore`, `SqlPromotionStore`, `SqlCatalog`, `SqlShiftStore`,
  `SqlTimeOffRequestStore`, `SqlAuditLogStore`), plus security-critical
  `PageModel` logic called directly: the checkout transaction (stock
  validation, rollback on failure), the IDOR guard on order confirmation, the
  admin self-protection guard, cart validation, login lockout, the
  email-confirmation login/register flow, and the GDPR inactivity-cleanup job.
- **End-to-end HTTP tests against a disposable `MarsvinDb_WebTest`
  database**, driving the real app in-process via `WebApplicationFactory<Program>`
  — the layer above can't see actual antiforgery/CSRF behavior or cookie
  flags, since calling a `PageModel` method directly skips the middleware
  pipeline entirely. These prove: a POST without an antiforgery token is
  rejected (400), a token from a different session is rejected (400) even
  though it's validly-formed, the auth cookie is `HttpOnly`, logging out
  actually expires that cookie and blocks a subsequent request to a protected
  page, and a full register → confirm-by-email → browse → log-out round trip
  works over real HTTP.

All three require LocalDB installed and running (`sqllocaldb info`).

## What's in it

| Page | Route | Who | What it shows |
|---|---|---|---|
| Front page | `/` | Everyone | Hero, bonded pairs, the four welfare questions, three essentials |
| Marsvinene | `/Marsvin` | Everyone | All animals, grouped into the pairs they're sold as |
| Profil | `/Marsvin/Details/{id}` | Everyone | One animal: breed, sex, age, colour, status, partner, buy button |
| Tilbehør | `/Tilbehor` | Everyone | 71 accessories: search, category tiles, filters for category/price/brand with counts, sorting, 24 per page; stock shown as Available/Low/Out (never a raw number), buy button |
| Mærker | `/Maerker` | Everyone | The brands the shop carries, each linking to its products |
| Pasningsguide / Foderliste | `/Pasningsguide`, `/PasningsguideHurtig`, `/Foderliste` | Everyone | Care guide, the short version, and the food list (the last two also as PDF) |
| Spørgsmål & svar | `/Faq` | Everyone | Frequently asked questions by topic |
| Om os / Betaling & levering / Privatliv | `/OmOs`, `/BetalingOgLevering`, `/Privatliv` | Everyone | About the shop, delivery rates and returns, the privacy policy |
| Kontakt | `/Kontakt` | Everyone | Contact details and a contact form (read by staff on `/Admin/Messages`) |
| Støt | `/Stoet` | Everyone | Donate money (demo - never charged) or products towards rehomed guinea pigs |
| Log ind / Opret konto | `/Account/Login`, `/Account/Register` | Everyone | Sign in, or self-register a Customer account — both require opening an emailed confirmation link before the session is signed in |
| Glemt adgangskode | `/Account/ForgotPassword`, `/Account/ResetPassword` | Everyone | Request and complete a password reset by email link |
| Min konto | `/Account/Profile` | Signed in | Update name/email/password, request time off (staff), order history + reorder (customers), delete account |
| Kurv / Betaling | `/Cart/Index`, `/Cart/Payment` | Everyone (not staff) | View cart, remove lines, choose pickup or shipping (+ carrier, each with price, destination, expected delivery, weight and parcel count), private or company purchase, choose card or MobilePay (demo), check out — no account needed, a guest just gives a name and email. Registering or logging in partway through carries the guest cart into the new session instead of losing it |
| Kvittering | `/Cart/Confirmation/{orderId}` | Everyone (not staff) | Order summary with status steps, a cancel button while the order can still be cancelled, and a confetti animation — only the buyer can view their own order (a guest's receipt is gated by a one-time id stamped into their own session at checkout, not an account) |
| Personale | `/Admin/Index` | Employee, Admin | Dashboard with role-appropriate links |
| Vagtplan | `/Admin/Schedule/Index` | Employee, Admin | See/assign shifts, approve or deny day-off requests |
| Butiksstyring | `/Admin/Shop/Index` | Employee, Admin | Hub for stock, orders, and (Admin) the catalog/promotions pages |
| Lager & status | `/Admin/Stock/Index` | Employee, Admin | Adjust accessory stock counts, change animal status |
| Ordrer | `/Admin/Orders/Index` | Employee, Admin | Every order placed in the shop, who placed it, and how it's being delivered; set status and tracking number (the buyer is emailed) |
| Beskeder / Donationer | `/Admin/Messages`, `/Admin/Donations` | Employee, Admin | What came in through the contact form and the donation page |
| Tilbehør (admin) | `/Admin/Products/Index` + `Edit` | Admin | Full CRUD on accessories |
| Marsvin (admin) | `/Admin/Animals/Index` + `Edit` | Admin | Full CRUD on guinea pigs |
| Kampagner | `/Admin/Promotions/Index` + `Edit` | Admin | Time-boxed discounts, shop-wide or per product |
| Konti | `/Admin/Users/Index` | Admin | Change roles, activate/deactivate accounts, create Employee/Admin accounts, export a customer's data |
| Logbog | `/Admin/AuditLog/Index` | Admin | Who changed what, and when — every admin/employee write action |

Every page also has an **EN / DA** toggle in the header — the site defaults
to Danish and switches to English client-side, remembering the choice per
browser.

Demo login credentials (seeded once, on a fresh database — see
`marsvin-web/DocumentationInformation/DATABASE-NOTES.txt`):

```
Admin:    admin@marsvin.dk     / Admin123!
Employee: employee@marsvin.dk  / Employee123!
Employee: employee2@marsvin.dk / Employee123!
Employee: employee3@marsvin.dk / Employee123!
Employee: employee4@marsvin.dk / Employee123!
```

Customers always self-register; there's no seeded customer account.

```
Models/          Product → StockProduct, Animal; ApplicationUser, CartLine, Order,
                 Promotion, Shift, TimeOffRequest, AuditLogEntry, Bilingual;
                 Shipping (ShippingCalculator), Company (CVR check), Inbox
                 (ContactMessage, Donation)
Data/            ICatalog / ICatalogAdmin (catalog), IUserAccountStore, ICartStore,
                 IOrderStore, IPromotionStore, IPendingLoginStore, IShiftStore,
                 ITimeOffRequestStore, IAuditLogStore, IInboxStore, IEmailSender —
                 each with a Sql* ADO.NET implementation; DbInitializer, Sql/schema.sql;
                 AuthCookiePrincipal (builds and re-validates the signed-in user);
                 the chat: ChatAssistants, ChatSkills, ShopKnowledge, ChatHelp
Pages/Account/   Register, Login, ConfirmLogin, VerifyTotp, ForgotPassword, ResetPassword,
                 CheckEmail, Logout, Profile, AccessDenied
Pages/Cart/      Index (view/add/remove), Payment (delivery choice + checkout), Confirmation
Pages/Admin/     Index, Schedule/, Shop/, Stock/, Orders/, Messages/, Donations/, Products/,
                 Animals/, Promotions/, Users/ (+ Export), AuditLog/
Pages/Shared/    _Layout.cshtml (header, footer, the chat), _Cavy.cshtml (the drawn guinea pig)
Pages/           the storefront and info pages, Chat (the chat's endpoint), Error/ServerError;
                 PageModelExtensions.cs — CurrentUserId/SignInAsync/IsSafeLocalUrl,
                 shared across every page model that needs them
wwwroot/css/     site.css — all the design tokens live at the top
wwwroot/img/     page photos, products/ (square, 640 px), animals/, brands/
wwwroot/js/      lang-toggle.js (the DA/EN switch), confirm-delete.js (safe confirm() dialogs),
                 cart-quantity.js (auto-submit qty changes), password-meter.js (live strength
                 meter), confetti.js (order confirmation), chat.js (the support chat),
                 payment.js (card-field formatting), auto-submit.js (sort menu),
                 mascot-pet.js, print-button.js, check-email.js, stock-tabs.js
marsvin-web.Tests/  xUnit tests for models, pages, and the SQL layer
```

## How the security-relevant parts work

- **Passwords**: hashed with ASP.NET Core's `PasswordHasher<T>` (PBKDF2-HMAC-SHA512,
  100k iterations, salted) — never stored or logged in plain text.
- **Login and registration both require email confirmation**, not just a
  password: a single-use, 15-minute link is emailed, and only opening it
  (`ConfirmLoginModel`) actually signs the session in — proof of inbox
  access, not just of knowing (or guessing) a password. Only the SHA-256
  hash of the token is ever stored (`PendingLogins`), the same way a
  password never is. Forgot-password reuses the same mechanism.
- **Sessions**: cookie authentication (`HttpOnly`, `SameSite=Lax`, `Secure`
  outside Development), checked server-side on every request via
  `[Authorize]`.
- **Sessions end when they should.** Changing or resetting a password ends
  every other session of that account, and so does "log out everywhere" on
  the profile page (a per-account *security stamp* in the cookie, replaced
  on those events and checked on every request) - so a stolen cookie stops
  working the moment the owner reacts. A session also ends after 30 minutes
  of inactivity, and after 8 hours however active it is, logging out clears
  the guest session too, and pages with personal data are sent `no-store`
  so the Back button can't show them after logout.
- **Authorization is server-side, not just hidden buttons.** Every role check
  happens in the `PageModel` (`[Authorize(Roles = "...")]`), re-checked per
  handler where a page is shared across roles (e.g. Admin/Schedule); an
  Employee who navigates straight to an Admin-only URL gets redirected to
  `/Account/AccessDenied`, not just a missing link in the nav.
- **Deny by default**: every page requires a signed-in user unless it's on
  the explicit public list in `Program.cs` (`AddRazorPages`), so a new page
  that forgets its `[Authorize]` ends up behind the login page, not public.
- **Role and account status are re-checked on every request**
  (`AuthCookiePrincipal.RevalidateAsync`): the auth cookie is only a snapshot
  from sign-in, so without this a deactivated, deleted, or demoted account
  would keep its old access for as long as the session was kept alive. Now
  it's gone on that account's very next request.
- **IDOR guard on orders**: `/Cart/Confirmation/{orderId}` looks up the order
  *and* checks it belongs to the logged-in user in the same query
  (`IOrderStore.FindForUser`) — you can't view someone else's order by
  guessing the ID. A guest checkout has no account for that ownership check
  to use, so it's gated a different way instead: the new order's id is
  stamped into that same browser's own session right after checkout, and
  only an exact match is ever trusted — not just "any guest order".
- **Shipping is priced server-side, never from the form.** The payment page
  shows each carrier's price, destination (door or parcel shop), expected
  delivery, total weight and parcel count (`ShippingCalculator` - demo rates,
  max. 20 kg per parcel), but `SqlOrderStore.Checkout` runs the same
  calculation itself from the catalog's own product weights and stores the
  result on the order; a tampered POST can pick a carrier, not a price.
- **Order status** (received → being prepared → sent/ready for pickup →
  delivered/picked up) is moved along by staff on `/Admin/Orders` (audit
  logged) and shown to the customer under Min konto.
- **A guest order can be moved into an account** created right afterwards,
  but only when both hold: it's the same browser session that placed the
  order, and the account's email is the one the order was placed with
  (`ClaimGuestOrderIntoAccount`) - so the next person at a shared computer
  can't sign up and inherit someone else's order and address.
- **Checkout re-validates inside a transaction, with row locks.** The cart
  can go stale between "add to cart" and "check out" (someone else buys the
  last unit, an animal gets marked not-for-sale); `SqlOrderStore.Checkout`
  re-checks stock/availability against the database inside the same
  transaction that records the order and decrements stock, using
  `UPDLOCK, HOLDLOCK` so two concurrent checkouts for the same last unit
  can't both pass validation, and rolls back the whole checkout rather than
  partially fulfilling it.
- **Self-protection on `/Admin/Users`**: an admin can't change their own role
  or deactivate their own account (only someone else's), and the last active
  admin account can't be demoted, deactivated, or deleted by anyone — so the
  shop can never end up with zero admins.
- **Rate limiting + login throttling**: `/Account/Login`, `/Account/Register`,
  and `/Account/ForgotPassword` are rate-limited per client IP (see the
  `"auth"` policy in `Program.cs`), and 5 failed login attempts for one email
  locks it out for 5 minutes (in-memory — fine for a demo, would need to be
  persisted for a real multi-instance deployment).
- **Security headers**: CSP, `X-Content-Type-Options`, `X-Frame-Options`, and
  `Referrer-Policy` are set on every response (see `Program.cs`).
- **Compression without BREACH**: the stylesheet, scripts and SVGs are
  compressed; HTML pages are not, because each carries a secret (the
  antiforgery token) next to text a visitor can influence, and compressing
  that over HTTPS is what the BREACH attack exploits.
- **Query parameters are allow-listed**: on `/Tilbehor` the category, brand,
  price range, sort order and page number are each matched against known
  values before use - nothing typed into the address bar is echoed back.
- **Error pages say what happened, and nothing more**: an expired form
  ("nothing was saved, ordered or paid"), too many attempts, no access and
  a missing page each have their own text; outside Development a crash
  shows a plain "something went wrong", never a stack trace.
- **The chat has nothing to leak**: no access to orders, accounts or staff,
  no knowledge of how the site is built, nothing stored, a rate limit, and
  answers written into the page as text, never as HTML.
- **Data minimisation**: "I am 16 or older" is a yes/no, not an age or a
  birth date; a phone number is kept with the order only.
- **No raw SQL string-building anywhere** — every query is a parameterised
  `SqlCommand`, including the admin CRUD forms.
- **CSRF**: Razor Pages validates the antiforgery token on every POST handler
  automatically; every form here uses `method="post"` with the framework's
  tag helpers, which include the token for free.
- **No inline `onclick`/`onsubmit` with interpolated data.** A destructive
  action's confirmation dialog reads its message from a `data-confirm`
  attribute (HTML-encoded by Razor) via a small delegated script
  (`confirm-delete.js`), rather than building a JavaScript string directly —
  the latter is decoded back to raw characters by the browser before being
  parsed as JS, which HTML-encoding alone doesn't protect against.
- **Audit log**: every admin/employee write action (price/stock/role
  changes, deletions, account creation) is recorded with who did it and when
  (`/Admin/AuditLog`), read-only after writing.
- **GDPR-style data retention**: a background service (`InactiveAccountCleanupService`)
  warns, then deletes, Customer accounts inactive for 2 years — see
  `/Privatliv` for the policy.

The database layer (`SqlCatalog`, `SqlUserAccountStore`, `SqlCartStore`,
`SqlOrderStore`, `SqlPromotionStore`, `SqlShiftStore`, `SqlTimeOffRequestStore`,
`SqlAuditLogStore`, `SqlPendingLoginStore`) uses plain ADO.NET with
parameterised `SqlCommand` throughout — no ORM. The `Product` / `StockProduct`
/ `Animal` hierarchy is shaped for the `ProductType` discriminator column.

## Design notes

The palette comes from the shop's own materials rather than a generic template:
dried hay (`#F0E7D2`), meadow green (`#2C4327`), and the bright fleece
(`#B83C6E`) that guinea pig owners actually line cages with.

Two structural decisions worth keeping if you rebuild the styling yourself:

**Animals get a profile, accessories get a shelf.** A guinea pig has its own
page, its name, age and personality, because it is an individual; a bag of
hay is a card in a filterable grid with a photo, a price and a stock level,
because it's stock. The visual difference carries the same information as
the class hierarchy.

**Bonded pairs are drawn as one unit** with a shared header strip. The rule that
guinea pigs are sold in pairs is the shop's whole character, so the layout states
it rather than burying it in a paragraph.

Each animal without a photo is drawn as inline SVG tinted from its own
`CoatPrimary` and `CoatSecondary` values, so a missing photo never looks like a
broken image. Set an animal's `PhotoUrl` to swap in a real photo — the
illustration stays as the fallback for every animal that doesn't have one yet,
and animals with a photo are listed first.

Photos are saved at the size they are shown, and nothing is loaded from another
site (no web fonts, no CDN), so pages stay light. The product photos, brand
names and logos belong to their owners — this is a school project that is not
online; don't reuse them in anything that is.

Responsive down to mobile, visible keyboard focus, and `prefers-reduced-motion`
respected.

## More documentation

Deeper reference docs live in `marsvin-web/DocumentationInformation/`:

- **`DATABASE-NOTES.txt`** — operational how-tos: starting the project,
  demo credentials, where the LocalDB files live, resetting the database.
- **`DATABASE-ARCHITECTURE.md`** — the data layer: schema-as-code, all 15
  tables and why they are shaped as they are, the store pattern,
  transactions/locking, seeding.
- **`DATABASE-CREATION-SCRIPT.md`** — the literal commands that build the
  database: what `dotnet run` does automatically, the full schema script,
  and the equivalent `sqlcmd` steps to do it by hand.
- **`WEBSITE-ARCHITECTURE.md`** — the web app itself: request pipeline,
  auth flow, site map, checkout and shipping, the bilingual DA/EN system,
  security practices, testing strategy, and how the support chat works.
