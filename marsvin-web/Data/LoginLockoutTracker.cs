namespace MarsvinWebExample.Data;

/// <summary>
/// Tracks failed login attempts per email and escalates the response the
/// way the course's rate-limiting slides recommend: a progressively longer
/// delay after each failure, a notification to the account owner once
/// there've been enough of them to look suspicious, and - past a hard
/// threshold - a lockout that a timer alone can't clear. That last one only
/// lifts when the account owner actually resets their password
/// (ResetPasswordModel calls Clear() on success), not just by waiting.
///
/// Registered as a Singleton so LoginModel (which registers failures and
/// reads the current state) and ResetPasswordModel (which clears it) share
/// one in-memory table. Demo-scale: doesn't survive an app restart or work
/// across multiple instances - a real deployment would persist this the
/// same way the per-IP rate limiter in Program.cs would need to.
/// </summary>
public sealed class LoginLockoutTracker
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);
    private const int NotifyAfterAttempts = 3;
    private const int RequireManualResetAfterAttempts = 5;

    private sealed class Entry
    {
        public int Attempts;
        public DateTime? DelayedUntil;
        public bool RequiresManualReset;
        public bool Notified;
        public DateTime LastSeenAt;
    }

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly object _lock = new();

    public bool IsLockedOut(string email)
    {
        lock (_lock)
        {
            PruneStaleEntries();
            if (!_entries.TryGetValue(email, out var entry)) return false;
            if (entry.RequiresManualReset) return true;
            return entry.DelayedUntil is DateTime until && until > DateTime.UtcNow;
        }
    }

    public bool RequiresManualReset(string email)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(email, out var entry) && entry.RequiresManualReset;
        }
    }

    /// <summary>
    /// Records one failed attempt and returns whether the account owner
    /// should be emailed about it - true the first time (and only the
    /// first time) this email crosses NotifyAfterAttempts. The caller sends
    /// the actual email; this only decides when.
    /// </summary>
    public bool RegisterFailedAttempt(string email)
    {
        lock (_lock)
        {
            PruneStaleEntries();
            if (!_entries.TryGetValue(email, out var entry))
            {
                entry = new Entry();
                _entries[email] = entry;
            }

            entry.Attempts++;
            entry.LastSeenAt = DateTime.UtcNow;

            // Grows with every failure (3^attempts seconds) up to the same
            // cap the hard lockout below effectively enforces, so the two
            // read as one continuously-escalating response rather than a
            // sudden jump from "instant retry" to "fully locked".
            var delaySeconds = Math.Min(Math.Pow(3, entry.Attempts), MaxDelay.TotalSeconds);
            entry.DelayedUntil = DateTime.UtcNow.AddSeconds(delaySeconds);

            if (entry.Attempts >= RequireManualResetAfterAttempts)
                entry.RequiresManualReset = true;

            if (entry.Attempts < NotifyAfterAttempts || entry.Notified) return false;
            entry.Notified = true;
            return true;
        }
    }

    /// <summary>
    /// Called on a successful login (clears the progressive delay - a
    /// legitimate sign-in shouldn't leave stray friction behind) and on a
    /// successful password reset (the only thing that can lift
    /// RequiresManualReset once it's set).
    /// </summary>
    public void Clear(string email)
    {
        lock (_lock)
        {
            _entries.Remove(email);
        }
    }

    // Called with _lock already held. Entries requiring a manual reset are
    // deliberately never pruned by time alone - only Clear() (i.e. an
    // actual password reset) removes those, or the lockout would silently
    // expire on its own after EntryTtl, defeating the point of requiring
    // one.
    private void PruneStaleEntries()
    {
        var cutoff = DateTime.UtcNow - EntryTtl;
        foreach (var key in _entries
                     .Where(kv => !kv.Value.RequiresManualReset && kv.Value.LastSeenAt < cutoff)
                     .Select(kv => kv.Key).ToList())
            _entries.Remove(key);
    }
}
