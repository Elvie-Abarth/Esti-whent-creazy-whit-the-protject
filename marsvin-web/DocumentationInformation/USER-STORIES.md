# User stories and where the code is

This doc lists our user stories and, for each one, where to find it in the
code: the route, the main files, and how we keep it safe. We kept the story
text in Danish the way we wrote it, and the pointers are in English to match the
rest of the docs. Paths are relative to the `marsvin-web/` folder.

For the deeper security write-up see `SECURITY-NOTES.txt`, and for how the whole
app fits together see `WEBSITE-ARCHITECTURE.md`.

## Quick map

| # | Story | Main route | Start here |
|---|---|---|---|
| 1 | Oprettelse af bruger | `/Account/Register` | `Pages/Account/Register.cshtml.cs` |
| 2 | Login | `/Account/Login` | `Pages/Account/Login.cshtml.cs` |
| 3 | Gennemse produkter | `/Marsvin`, `/Tilbehor` | `Pages/Tilbehor/Index.cshtml.cs` |
| 4 | Lægge varer i kurven | `/Cart` | `Pages/Cart/Index.cshtml.cs` |
| 5 | Checkout og betaling | `/Cart/Payment` | `Pages/Cart/Payment.cshtml.cs` |
| 6 | Ændring af adgangskode | `/Account/Profile` | `Pages/Account/Profile.cshtml.cs` |
| 7 | Søgning | `/Tilbehor?q=...` | `Pages/Tilbehor/Index.cshtml.cs` |
| 8 | Ordrehistorik | `/Account/Profile` | `Data/SqlOrderStore.cs` |
| 9 | Administration af produkter | `/Admin/Products`, `/Admin/Animals` | `Pages/Admin/Products/` |
| 10 | Logning og overvågning | `/Admin/SecurityLog`, `/Admin/AuditLog` | `Data/SqlSecurityEventStore.cs` |
| 11 | Password-hjælp | (on the sign-up and reset forms) | `Models/PasswordPolicy.cs` |

---

## 1. Oprettelse af bruger

> Som bruger ønsker jeg at oprette en konto, så jeg kan gemme mine personlige
> oplysninger og købe varer, samtidig med at mine data er beskyttet mod
> uautoriseret adgang.

**Where to find it**
- Route `/Account/Register`: `Pages/Account/Register.cshtml` and
  `Pages/Account/Register.cshtml.cs`.
- Signing up does not log you in by itself. We email a single-use confirmation
  link, and opening it in `Pages/Account/ConfirmLogin.cshtml.cs` is what creates
  the session.
- The account is written by `Data/SqlUserAccountStore.cs`. The confirmation
  token lives in `Data/SqlPendingLoginStore.cs`, and the mail goes out through
  `Data/IEmailSender.cs` (`Data/SmtpEmailSender.cs` or
  `Data/LoggingEmailSender.cs`).

**How we keep it safe**
- We hash the password with ASP.NET Core's `PasswordHasher` (PBKDF2), so we
  never store it in plain text. The rule it must pass is one shared policy,
  `Models/PasswordPolicy.cs`.
- Registering with an email that already exists gives the same "check your
  email" answer as a new sign-up, so the form cannot be used to find out who has
  an account.
- The page is rate limited per IP through the `"auth"` policy in `Program.cs`.

## 2. Login

> Som bruger ønsker jeg at logge ind på min konto, så jeg kan se mine tidligere
> ordrer, samtidig med at mine loginoplysninger er sikre mod brute force-angreb.

**Where to find it**
- Route `/Account/Login`: `Pages/Account/Login.cshtml.cs`. The second step is
  either `Pages/Account/ConfirmLogin.cshtml.cs` (email link) or
  `Pages/Account/VerifyTotp.cshtml.cs` (authenticator code).
- The signed-in user is built and re-checked in `Data/AuthCookiePrincipal.cs`.

**How we keep it safe (against brute force)**
- `Data/LoginLockoutTracker.cs` counts failures per email (`IsLockedOut`,
  `RegisterFailedAttempt`). Each failure adds a delay, the 3rd emails the owner a
  warning, and the 5th locks the account until a password reset.
- The login page is also rate limited per IP (the `"auth"` policy in
  `Program.cs`).
- A correct password alone never signs you in; the email link or the TOTP code
  is always required.

## 3. Gennemse produkter

> Som bruger ønsker jeg at kunne se en liste over tilgængelige produkter, så jeg
> kan vælge, hvad jeg vil købe, samtidig med at mine forespørgsler er sikre mod
> angreb.

**Where to find it**
- Guinea pigs: `/Marsvin` (`Pages/Marsvin/Index.cshtml.cs`) and one animal at
  `/Marsvin/Details/{id}` (`Pages/Marsvin/Details.cshtml.cs`).
- Accessories: `/Tilbehor` (`Pages/Tilbehor/Index.cshtml.cs`).
- The data comes from `Data/SqlCatalog.cs` through the `ICatalog` interface.

**How we keep it safe**
- Every query in `Data/SqlCatalog.cs` is a parameterised `SqlCommand`, so a value
  in the URL can never become part of the SQL.
- The filter, sort and page values on `/Tilbehor` are matched against a known
  list before we use them (see `Pages/Tilbehor/Index.cshtml.cs`), so nothing
  typed into the address bar is echoed back.

## 4. Lægge varer i kurven

> Som bruger ønsker jeg at kunne tilføje varer til min indkøbskurv, så jeg kan
> fortsætte til kassen, samtidig med at kurven ikke kan manipuleres af andre.

**Where to find it**
- Route `/Cart`: `Pages/Cart/Index.cshtml.cs`.
- A signed-in customer's cart is in the database (`Data/SqlCartStore.cs`). A
  guest's cart lives in that one browser session (`Data/SessionCartStore.cs`).

**How we keep it safe**
- Every change to the cart is a POST with an antiforgery token (Razor Pages adds
  and checks it), so another site cannot change your cart on your behalf.
- A guest cart is tied to that guest's own session, and a customer cart to that
  account, so one person cannot reach another person's cart.

## 5. Checkout og betaling

> Som bruger ønsker jeg at gennemføre mit køb ved at betale for varerne, så jeg
> kan få min ordre, samtidig med at mine betalingsoplysninger holdes sikre.

**Where to find it**
- Route `/Cart/Payment`: `Pages/Cart/Payment.cshtml.cs`. The receipt is
  `/Cart/Confirmation/{orderId}` (`Pages/Cart/Confirmation.cshtml.cs`).
- The order is written by `Data/SqlOrderStore.cs` (the `Checkout` method).
  Shipping is priced by `ShippingCalculator` in `Models/Shipping.cs`.

**How we keep it safe**
- We work out the shipping price on the server from the catalog's own weights,
  never from the form, so a tampered POST can pick a carrier but not a price.
- `Checkout` re-checks stock inside one transaction using `UPDLOCK, HOLDLOCK`, so
  two people cannot both buy the same last unit, and it rolls the whole checkout
  back rather than half-finishing it.
- Payment is a demo and is never charged, so there are no real card details to
  keep.

## 6. Ændring af adgangskode

> Som bruger ønsker jeg at kunne ændre min adgangskode, så jeg kan sikre min
> konto, hvis min adgangskode er blevet kompromitteret.

**Where to find it**
- Change it while signed in at `/Account/Profile`
  (`Pages/Account/Profile.cshtml.cs`).
- If you forgot it, reset by email link: `/Account/ForgotPassword`
  (`Pages/Account/ForgotPassword.cshtml.cs`) then `/Account/ResetPassword`
  (`Pages/Account/ResetPassword.cshtml.cs`).

**How we keep it safe**
- The new password must pass the same `Models/PasswordPolicy.cs` rules, checked on
  the server.
- Changing or resetting the password replaces the account's security stamp (in
  `Data/SqlUserAccountStore.cs` and re-checked in `Data/AuthCookiePrincipal.cs`),
  which ends every other session at once. So if someone else had a stolen cookie,
  it stops working the moment you change your password.

## 7. Søgning

> Som bruger ønsker jeg at søge efter produkter i webshoppen, så jeg hurtigt kan
> finde, hvad jeg leder efter, samtidig med at søgningen er sikker.

**Where to find it**
- The search box is on `/Tilbehor` and reads the `q` value in
  `Pages/Tilbehor/Index.cshtml.cs`. The matching happens in `Data/SqlCatalog.cs`.

**How we keep it safe**
- The search word goes to the database as a parameter, never glued into the SQL
  text, so something like `' OR 1=1` is just a word that matches nothing.
- When the word is shown back on the page, Razor HTML-encodes it, so a search for
  `<script>` shows as text and never runs (no reflected XSS).

## 8. Ordrehistorik

> Som bruger ønsker jeg at kunne se mine tidligere ordrer, så jeg kan holde styr
> på mine køb, samtidig med at mine data ikke er tilgængelige for andre.

**Where to find it**
- A customer sees their orders on `/Account/Profile`
  (`Pages/Account/Profile.cshtml.cs`), and one receipt at
  `/Cart/Confirmation/{orderId}` (`Pages/Cart/Confirmation.cshtml.cs`).
- The lookups are in `Data/SqlOrderStore.cs`.

**How we keep it safe (no IDOR)**
- `Data/SqlOrderStore.cs` looks an order up with both the order id and the
  signed-in user's id in the same query (`FindForUser`), so guessing another
  order number just shows "not found". You can only see your own orders.
- A guest's receipt is tied to that guest's own session, not to an account.

## 9. Administration af produkter

> Som administrator ønsker jeg at kunne tilføje, redigere og slette produkter, så
> jeg kan holde webshoppen opdateret, samtidig med at kun autoriserede brugere
> har adgang til disse funktioner.

**Where to find it**
- Accessories: `/Admin/Products` and `/Admin/Products/Edit`
  (`Pages/Admin/Products/Index.cshtml.cs`, `Pages/Admin/Products/Edit.cshtml.cs`).
- Guinea pigs: `/Admin/Animals` and `/Admin/Animals/Edit`
  (`Pages/Admin/Animals/`).
- The writes go through `ICatalogAdmin` in `Data/SqlCatalog.cs`.

**How we keep it safe (only authorised users)**
- These pages carry `[Authorize(Roles = "Admin")]`, checked on the server before
  any page code runs, not by hiding buttons.
- Deny by default: a page is public only if it is on the explicit list in
  `Program.cs` (`AuthorizeFolder("/")` plus `AllowAnonymousToPage(...)`), so a new
  admin page that forgets its attribute still ends up behind the login.
- Every add, edit and delete is written to the audit log
  (`Data/SqlAuditLogStore.cs`) with who did it and when.

## 10. Logning og overvågning

> Som systemadministrator ønsker jeg at kunne overvåge logfiler, så jeg kan
> identificere og reagere på mistænkelig aktivitet, samtidig med at logdata er
> beskyttet mod uautoriseret adgang.

**Where to find it**
- Attack attempts: `/Admin/SecurityLog` (`Pages/Admin/SecurityLog/Index.cshtml.cs`).
  Staff actions: `/Admin/AuditLog` (`Pages/Admin/AuditLog/Index.cshtml.cs`).
- The security side is `Models/SecurityEvent.cs`, `Data/SqlSecurityEventStore.cs`,
  `Data/SecurityMonitor.cs` (records events) and `Data/SecurityAlerter.cs` (emails
  the admins when a pattern crosses a threshold). The hooks that feed it live in
  `Program.cs`.
- The audit side is `Data/SqlAuditLogStore.cs`.

**How we keep it safe (logs protected)**
- Both logs are `[Authorize(Roles = "Admin")]`, so only an admin can read them.
- We record metadata only (time, email, client IP, path), never a password or a
  token.
- The audit log has no edit handler, so it is read-only once written.
- This is honest about what it is: it alerts on attempts seen in our own logs, not
  on a confirmed breach.

## 11. Password-hjælp

> Som bruger ønsker jeg at få noget hjælp til at sammensætte mit password, så jeg
> ikke skal bekymre mig om at mine oplysninger kompromitteres af andre.

**Where to find it**
- The rules are in `Models/PasswordPolicy.cs`. The list of common passwords we
  reject is `Models/CommonPasswordList.cs` (loaded from
  `Data/CommonPasswords.txt`).
- The live strength meter on the form is `wwwroot/js/password-meter.js`. It shows
  on the sign-up, reset and profile pages.

**How it helps**
- The meter shows the exact five rules the server will enforce (length, upper and
  lower case, a digit, a symbol, and "not an obvious password"), so what it calls
  "strong" is precisely what the server accepts.
- Because the same `PasswordPolicy` runs on the server too, the help is real: a
  weak password is refused even if someone skips the meter.
