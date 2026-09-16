using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class LoginLockoutTrackerTests
{
    [Fact]
    public void RegisterFailedAttempt_FirstAttempt_LocksOutBriefly()
    {
        // Progressive delay applies from the very first failure, not just
        // once a hard threshold is hit - a single wrong password should
        // already impose a short (but real) delay before the next try.
        var tracker = new LoginLockoutTracker();
        var email = "person@example.com";

        tracker.RegisterFailedAttempt(email);

        Assert.True(tracker.IsLockedOut(email));
        Assert.False(tracker.RequiresManualReset(email));
    }

    [Fact]
    public void RegisterFailedAttempt_ThirdAttempt_SignalsNotifyOnceOnly()
    {
        var tracker = new LoginLockoutTracker();
        var email = "person@example.com";

        Assert.False(tracker.RegisterFailedAttempt(email)); // 1st
        Assert.False(tracker.RegisterFailedAttempt(email)); // 2nd
        Assert.True(tracker.RegisterFailedAttempt(email));  // 3rd - crosses the threshold
        Assert.False(tracker.RegisterFailedAttempt(email)); // 4th - already notified once
    }

    [Fact]
    public void RegisterFailedAttempt_FifthAttempt_RequiresManualReset()
    {
        var tracker = new LoginLockoutTracker();
        var email = "person@example.com";

        for (var i = 0; i < 5; i++)
            tracker.RegisterFailedAttempt(email);

        Assert.True(tracker.IsLockedOut(email));
        Assert.True(tracker.RequiresManualReset(email));
    }

    [Fact]
    public void Clear_AfterManualResetRequired_LiftsTheLockout()
    {
        // The only thing that should be able to lift a RequiresManualReset
        // lockout is an actual password reset (ResetPasswordModel calling
        // Clear()) - not time passing on its own.
        var tracker = new LoginLockoutTracker();
        var email = "person@example.com";

        for (var i = 0; i < 5; i++)
            tracker.RegisterFailedAttempt(email);
        Assert.True(tracker.IsLockedOut(email));

        tracker.Clear(email);

        Assert.False(tracker.IsLockedOut(email));
        Assert.False(tracker.RequiresManualReset(email));
    }

    [Fact]
    public void IsLockedOut_UnknownEmail_IsFalse()
    {
        var tracker = new LoginLockoutTracker();

        Assert.False(tracker.IsLockedOut("never-seen@example.com"));
        Assert.False(tracker.RequiresManualReset("never-seen@example.com"));
    }
}
