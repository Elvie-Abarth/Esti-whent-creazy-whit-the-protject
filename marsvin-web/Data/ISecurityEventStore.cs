using MarsvinWebExample.Models;

namespace MarsvinWebExample.Data;

/// <summary>
/// Records and reads security events (see <see cref="SecurityEvent"/>).
/// Append-and-read only, like the audit log - there is no update, and the
/// only delete is the retention prune. The count helpers are what the
/// threshold alerts (SecurityAlerter) ask.
/// </summary>
public interface ISecurityEventStore
{
    void Record(SecurityEvent securityEvent);

    IReadOnlyList<SecurityEvent> GetRecent(int count);

    /// <summary>How many events of the given type(s) since <paramref name="since"/> (any type when none are given).</summary>
    int CountSince(DateTime since, params SecurityEventType[] types);

    /// <summary>How many events from one IP since <paramref name="since"/>, of the given type(s).</summary>
    int CountFromIpSince(string ipAddress, DateTime since, params SecurityEventType[] types);

    /// <summary>How many *distinct* emails one IP has aimed the given event type at since <paramref name="since"/> - the credential-stuffing signal.</summary>
    int DistinctEmailsFromIpSince(string ipAddress, DateTime since, SecurityEventType type);

    /// <summary>Deletes events older than the cutoff (retention). Returns how many rows went.</summary>
    int DeleteOlderThan(DateTime cutoff);
}
