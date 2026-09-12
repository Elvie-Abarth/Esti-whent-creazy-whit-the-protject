using MarsvinWebExample.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("MarsvinDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:MarsvinDb in configuration.");

// Creates the LocalDB database and schema on first run, and seeds it from
// DemoCatalog's data. Every request after that reads through SqlCatalog via
// plain ADO.NET - no ORM, every query parameterised.
DbInitializer.EnsureCreatedAndSeeded(connectionString);
builder.Services.AddSingleton<ICatalog>(_ => new SqlCatalog(connectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
