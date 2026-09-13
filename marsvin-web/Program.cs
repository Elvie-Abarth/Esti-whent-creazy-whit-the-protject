using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

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
builder.Services.AddScoped<ICartStore>(_ => new SqlCartStore(connectionString));
builder.Services.AddScoped<IOrderStore>(_ => new SqlOrderStore(connectionString));
builder.Services.AddScoped<IPromotionStore>(_ => new SqlPromotionStore(connectionString));
builder.Services.AddScoped<IPendingLoginStore>(_ => new SqlPendingLoginStore(connectionString));
builder.Services.AddScoped<IShiftStore>(_ => new SqlShiftStore(connectionString));
builder.Services.AddScoped<ITimeOffRequestStore>(_ => new SqlTimeOffRequestStore(connectionString));

// Host/Port/FromName are plain config; Username/Password are meant to come
// from `dotnet user-secrets` (or real environment variables in production),
// never from a file that gets committed.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

// Exposes the top-level Program to WebApplicationFactory<Program> in the
// test project, which needs a public type to boot the app in-process.
public partial class Program;
