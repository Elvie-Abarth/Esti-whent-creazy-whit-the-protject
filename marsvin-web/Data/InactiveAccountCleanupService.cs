namespace MarsvinWebExample.Data;

/// <summary>
/// Enforces the retention policy described on /Privatliv: a Customer account
/// with no login for 2 years is deleted automatically, not kept "just in
/// case" - GDPR's data-minimisation principle, not just a line of text on a
/// page. Employee/Admin accounts are never touched by this (see
/// SqlUserAccountStore.DeleteInactiveCustomers).
///
/// Also emails a warning about 2 months, and again about 1 month, before that
/// happens - with instructions on how to keep the account (just log in).
/// Silently deleting an account nobody was told was at risk wouldn't satisfy
/// GDPR's fairness/transparency principle, even with the policy documented
/// on Privatliv.
///
/// Takes a connection string directly rather than IUserAccountStore via DI:
/// that interface is registered Scoped (fine for a per-request PageModel),
/// but this is a singleton BackgroundService with no request to scope to -
/// juggling an IServiceScopeFactory for one simple, stateless ADO.NET class
/// would be more machinery than the problem needs. IEmailSender is already a
/// singleton, so that one's injected normally.
/// </summary>
public sealed class InactiveAccountCleanupService(
    string connectionString,
    string appBaseUrl,
    IEmailSender emailSender,
    ILogger<InactiveAccountCleanupService> logger)
    : BackgroundService
{
    public static readonly TimeSpan InactivityThreshold = TimeSpan.FromDays(365 * 2);
    private static readonly TimeSpan TwoMonthWarningLeadTime = TimeSpan.FromDays(60);
    private static readonly TimeSpan OneMonthWarningLeadTime = TimeSpan.FromDays(30);

    // Daily is frequent enough for thresholds measured in months/years, and cheap
    // enough (a handful of queries, usually zero rows) not to matter if it's not.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync();
            }
            catch (Exception ex)
            {
                // A transient DB or SMTP hiccup shouldn't take down this background
                // loop for the rest of the app's lifetime - log it and try again tomorrow.
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

    private async Task RunOnceAsync()
    {
        var users = new SqlUserAccountStore(connectionString);
        var now = DateTime.UtcNow;

        // Delete first: an account already past the full threshold also falls
        // within both warning windows below, and there's no point emailing
        // someone whose account is about to disappear in the same pass.
        var deleted = users.DeleteInactiveCustomers(now - InactivityThreshold);
        if (deleted.Count > 0)
        {
            logger.LogInformation(
                "Inactive-account cleanup deleted {Count} customer account(s) with no login in over 2 years.",
                deleted.Count);
        }

        // 1-month pass first: an account that skipped the 2-month window entirely
        // (e.g. the service was down) should still get the more urgent warning,
        // not silently miss both.
        await WarnAsync(users, stage: 2, leadTime: OneMonthWarningLeadTime, now, monthsLeftText: "en måned");
        await WarnAsync(users, stage: 1, leadTime: TwoMonthWarningLeadTime, now, monthsLeftText: "to måneder");
    }

    private async Task WarnAsync(SqlUserAccountStore users, byte stage, TimeSpan leadTime, DateTime now, string monthsLeftText)
    {
        var lastActiveBefore = now - InactivityThreshold + leadTime;
        var needsWarning = users.GetCustomersNeedingInactivityWarning(lastActiveBefore, stage);

        foreach (var account in needsWarning)
        {
            var deletionDate = account.LastActiveAt + InactivityThreshold;
            var subject = $"Din Marsvin-konto slettes om ca. {monthsLeftText}, medmindre du logger ind";
            var body =
                $"""
                Hej {account.DisplayName},

                Din Marsvin-konto har ikke været i brug i lang tid. Medmindre du logger ind inden {deletionDate:d MMMM yyyy}, bliver kontoen og dine kontooplysninger slettet automatisk - i tråd med vores 2-års-politik for inaktive konti ({appBaseUrl}/Privatliv).

                Sådan undgår du sletning: log blot ind på din konto én gang inden da.
                {appBaseUrl}/Account/Login

                Dine tidligere ordrer bevares under alle omstændigheder, uanset om kontoen slettes.

                Venlig hilsen
                Marsvin
                """;

            try
            {
                await emailSender.SendAsync(account.Email, subject, body);
                users.SetInactivityWarningStage(account.UserId, stage);
            }
            catch (Exception ex)
            {
                // Leave the stage as-is so this account is retried on the next run
                // instead of silently marking a warning as sent that never arrived.
                logger.LogError(ex, "Failed to send inactivity warning to user {UserId}.", account.UserId);
            }
        }

        if (needsWarning.Count > 0)
        {
            logger.LogInformation(
                "Inactive-account cleanup sent {Count} '{MonthsLeft} left' warning email(s).",
                needsWarning.Count, monthsLeftText);
        }
    }
}
