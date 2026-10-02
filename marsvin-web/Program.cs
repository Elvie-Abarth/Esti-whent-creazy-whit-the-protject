using System.Threading.RateLimiting;
using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using QuestPDF.Infrastructure;

// Community licence: free for this kind of project (small team, not
// generating revenue) - required by QuestPDF before GeneratePdf() will run.
// See Data/FoodListPdfDocument.cs, used by the /Foderliste/Pdf download.
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Framework defaults are max-age=30 days, no includeSubDomains, no preload -
// tightened to the standard recommendation (2 years, cover subdomains too,
// eligible for browsers' built-in HSTS preload lists) so a user's very first
// visit over a stale HTTP link/bookmark can't be downgrade-attacked once this
// is deployed for real. Only takes effect outside Development (see UseHsts()
// below) - HSTS can't work against http://localhost without a real cert anyway.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(730);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Only used for a guest's cart (SessionCartStore below) - a signed-in
// customer's cart lives in dbo.CartItems instead, keyed by their real
// UserId, which survives far longer than this 60-minute idle window and
// works across devices. IsEssential: true is what it sounds like under
// the GDPR ePrivacy rules covered elsewhere in this app's own security
// write-up - this cookie carries no tracking/preference data, just "which
// session", so it's exempt from needing cookie-consent like an analytics
// cookie would. Same dev/prod Secure split as the auth cookie just below,
// and the same __Host- prefix outside Development for the same reason
// (ASVS 3.4.4) - both need an actually-secure connection to work at all,
// which the plain-HTTP local launch profile isn't.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    if (!builder.Environment.IsDevelopment())
        options.Cookie.Name = "__Host-MarsvinSession";
});

// Applied via [EnableRateLimiting("auth")] to Login/Register/ForgotPassword -
// caps how many attempts one client can make per minute, independent of
// (and in addition to) LoginModel's own per-email lockout: that alone can't
// stop an attacker who already knows a victim's email from re-locking it
// indefinitely, or one client from spraying attempts across many different
// emails. Partitioned per client IP, so one noisy client never throttles
// anyone else.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetSlidingWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new SlidingWindowRateLimiterOptions
        {
            // Generous enough that everyone behind one shared IP (a school,
            // an office, a test suite hammering these endpoints in a tight
            // loop) doesn't throttle each other under normal use, while still
            // stopping the thousands-of-attempts-per-minute a real
            // credential-stuffing or spam script would make.
            PermitLimit = 50,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0
        }));
});

var connectionString = builder.Configuration.GetConnectionString("MarsvinDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:MarsvinDb in configuration.");

// Creates the LocalDB database and schema on first run (catalog tables are
// reseeded from DemoCatalog every run; Users/Orders/CartItems/Promotions are
// created once and never touched again - see DbInitializer.cs). Every
// request after that reads and writes through plain ADO.NET, parameterised.
DbInitializer.EnsureCreatedAndSeeded(connectionString);

// SqlCatalog is scoped, not singleton: ICatalogAdmin lets admin/employee
// edit the catalog mid-run, and a cached singleton would go stale until
// restart. Registering the concrete type once and mapping both interfaces
// to it keeps a single instance (and connection) per request.
builder.Services.AddScoped(_ => new SqlCatalog(connectionString));
builder.Services.AddScoped<ICatalog>(sp => sp.GetRequiredService<SqlCatalog>());
builder.Services.AddScoped<ICatalogAdmin>(sp => sp.GetRequiredService<SqlCatalog>());

builder.Services.AddScoped<IUserAccountStore>(_ => new SqlUserAccountStore(connectionString));

// A signed-in customer's cart lives in dbo.CartItems, keyed by their real
// UserId - a guest has no UserId at all, so their cart lives in session
// instead (SessionCartStore, backed by AddSession below). Every Cart/*
// page model just asks for ICartStore and calls cart.GetLines(...) exactly
// the same way either way; which implementation it actually gets is
// decided once, here, per request, off whether anyone's signed in.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICartStore>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
    return httpContext.User.Identity?.IsAuthenticated == true
        ? new SqlCartStore(connectionString)
        : new SessionCartStore(httpContext.Session, sp.GetRequiredService<ICatalog>());
});

builder.Services.AddScoped<IOrderStore>(_ => new SqlOrderStore(connectionString));
builder.Services.AddScoped<IPromotionStore>(_ => new SqlPromotionStore(connectionString));
builder.Services.AddScoped<IPendingLoginStore>(_ => new SqlPendingLoginStore(connectionString));
builder.Services.AddScoped<IShiftStore>(_ => new SqlShiftStore(connectionString));
builder.Services.AddScoped<ITimeOffRequestStore>(_ => new SqlTimeOffRequestStore(connectionString));
builder.Services.AddScoped<IAuditLogStore>(_ => new SqlAuditLogStore(connectionString));

// Singleton, not Scoped - the whole point is tracking failed attempts
// across requests. LoginModel and ResetPasswordModel share this one
// instance (see LoginLockoutTracker's own doc comment for why).
builder.Services.AddSingleton<LoginLockoutTracker>();

// Host/Port/FromName are plain config; Username/Password are meant to come
// from `dotnet user-secrets` (or real environment variables in production),
// never from a file that gets committed. Without them set, SmtpEmailSender
// can't authenticate - and since login is email-link-only, that would make
// the app 500 on every login attempt for anyone who clones the repo and runs
// it before setting up user-secrets. LoggingEmailSender logs the link
// instead, so the app is usable out of the box; set Email:Username to switch
// to sending real mail.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
if (string.IsNullOrWhiteSpace(builder.Configuration["Email:Username"]))
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// SiteKey is public config; SecretKey follows the same user-secrets-only
// rule as Email:Password. GoogleRecaptchaVerifier skips verification when
// SecretKey is empty, and _Recaptcha.cshtml skips rendering the widget when
// SiteKey is empty, so Login/Register/ForgotPassword stay usable out of the
// box without a developer's own Google reCAPTCHA keys.
builder.Services.Configure<RecaptchaOptions>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddHttpClient<IRecaptchaVerifier, GoogleRecaptchaVerifier>();

// Used to build absolute links (login-confirmation, inactivity-warning
// emails) from a BackgroundService, which has no HttpContext/Request to read
// the real scheme+host from the way a PageModel can.
var appBaseUrl = builder.Configuration["App:BaseUrl"] ?? "http://localhost:5080";

// See /Privatliv for the policy this enforces: a Customer account with no
// login for 2 years is deleted automatically, with warning emails first.
builder.Services.AddHostedService(sp =>
    new InactiveAccountCleanupService(
        connectionString, appBaseUrl, sp.GetRequiredService<IEmailSender>(),
        sp.GetRequiredService<ILogger<InactiveAccountCleanupService>>()));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        // Always-Secure would also block the cookie over the plain-HTTP
        // "http" launch profile this project runs under locally - SameAsRequest
        // only in Development keeps that working, while a real (non-Development)
        // deployment - which should only ever be reached over HTTPS anyway,
        // given UseHttpsRedirection/UseHsts below - never sends the session
        // cookie in the clear.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // The __Host- prefix (ASVS 3.4.4) makes the browser itself refuse to
        // ever set this cookie unless Secure is set, Path is "/", and no
        // Domain attribute is present (true above - Domain is never set at
        // all) - it's what stops a subdomain, or another app sharing this
        // domain, from being able to plant or read a same-named cookie that
        // this app would then trust. Only outside Development: the prefix
        // requires an actually-secure (HTTPS) connection to work at all,
        // which the "http" localhost launch profile isn't, and the browser
        // would silently refuse to store the cookie - breaking login - if
        // this were applied there too.
        if (!builder.Environment.IsDevelopment())
            options.Cookie.Name = "__Host-MarsvinAuth";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Unexpected exceptions land on ServerError (an honest "something went
    // wrong" page) - kept separate from Error, which is the 404/"page
    // doesn't exist" page below and would otherwise tell a confused user
    // their crash was a broken link.
    app.UseExceptionHandler("/ServerError");
    app.UseHsts();
}

// Re-executes the pipeline against /Error for any response that reaches here
// with an error status code and no body yet - an unmatched route, or an
// explicit NotFound()/Forbid() result from a page handler - instead of
// leaving the visitor looking at a blank page.
app.UseStatusCodePagesWithReExecute("/Error");

// Defence in depth alongside Razor's automatic HTML-encoding and the
// anti-forgery token on every POST: none of these cost anything to add, and
// each blocks a whole category of attack the app would otherwise rely on
// every browser + every future page getting right on its own.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // No inline <style>/style="" or inline <script> anywhere in the project
    // (confirmed by grep) - so both script-src and style-src stay locked to
    // 'self' with no 'unsafe-inline'/'unsafe-eval'. The two google.com/
    // gstatic.com additions are only reached at all when a Recaptcha:SiteKey
    // is configured (see _Recaptcha.cshtml) - reCAPTCHA's widget script and
    // the iframe it renders both need to load from Google's own origins.
    headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self' https://www.google.com https://www.gstatic.com; " +
        "style-src 'self'; frame-src https://www.google.com; " +
        "img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self';";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

// Exposes the top-level Program to WebApplicationFactory<Program> in the
// test project, which needs a public type to boot the app in-process.
public partial class Program;
