using MarsvinWebExample.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarsvinWebExample.Tests;

internal static class SecurityTestHelpers
{
    /// <summary>
    /// A SecurityMonitor wired to the test database. Its admin-alert emails go
    /// to a throwaway recorder, so a threshold crossing during an unrelated
    /// test never pollutes that test's own assertions on login/confirmation
    /// email. Tests that want to see the alert build their own (see
    /// SecurityAlerterTests).
    /// </summary>
    public static SecurityMonitor Monitor(string connectionString) =>
        new(new SqlSecurityEventStore(connectionString),
            new SqlUserAccountStore(connectionString),
            new RecordingEmailSender(),
            new SecurityAlerter("http://localhost", NullLogger<SecurityAlerter>.Instance),
            NullLogger<SecurityMonitor>.Instance);
}
