namespace MarsvinWebExample.Data;

/// <summary>
/// Bound from the "Email" config section. Host/Port/FromName live in
/// appsettings.json; Username/Password are set locally with
/// `dotnet user-secrets set Email:Username ...` / `Email:Password ...` so the
/// mailbox credentials never end up in a file that gets committed.
/// </summary>
public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string FromName { get; set; } = "Marsvin";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
