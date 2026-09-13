namespace MarsvinWebExample.Data;

/// <summary>
/// Enforces the retention policy described on /Privatliv: a Customer account
/// with no login for 2 years is deleted automatically, not kept "just in
/// case" - GDPR's data-minimisation principle, not just a line of text on a
/// page. Employee/Admin accounts are never touched by this (see
/// SqlUserAccountStore.DeleteInactiveCustomers).
///
/// Takes a connection string directly rather than IUserAccountStore via DI:
/// that interface is registered Scoped (fine for a per-request PageModel),
/// but this is a singleton BackgroundService with no request to scope to -
/// juggling an IServiceScopeFactory for one simple, stateless ADO.NET class
/// would be more machinery than the problem needs.
/// </summary>
public sealed class InactiveAccountCleanupService(string connectionString, ILogger<InactiveAccountCleanupService> logger)
    : BackgroundService
{
    public static readonly TimeSpan InactivityThreshold = TimeSpan.FromDays(365 * 2);

    // Daily is frequent enough for a threshold measured in years, and cheap
    // enough (one query, usually zero rows) not to matter if it's not.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var users = new SqlUserAccountStore(connectionString);
                var cutoff = DateTime.UtcNow - InactivityThreshold;
                var deleted = users.DeleteInactiveCustomers(cutoff);

                if (deleted.Count > 0)
                {
                    logger.LogInformation(
                        "Inactive-account cleanup deleted {Count} customer account(s) with no login in over 2 years.",
                        deleted.Count);
                }
            }
            catch (Exception ex)
            {
                // A transient DB hiccup shouldn't take down this background loop for
                // the rest of the app's lifetime - log it and just try again tomorrow.
                logger.LogError(ex, "Inactive-account cleanup failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // App is shutting down.
            }
        }
    }
}
