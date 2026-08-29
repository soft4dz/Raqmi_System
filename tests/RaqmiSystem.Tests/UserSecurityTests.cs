using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Tests;

public sealed class UserSecurityTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterFailedLogin_accumulates_attempts_within_the_window()
    {
        var user = CreateUser();

        user.RegisterFailedLogin(BaseTime);
        user.RegisterFailedLogin(BaseTime.AddMinutes(1));
        user.RegisterFailedLogin(BaseTime.AddMinutes(2));

        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(BaseTime.AddMinutes(2)));
    }

    [Fact]
    public void RegisterFailedLogin_does_not_lock_the_account_after_four_attempts()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            user.RegisterFailedLogin(BaseTime.AddSeconds(attempt));
        }

        Assert.Equal(4, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(BaseTime.AddSeconds(4)));
    }

    [Fact]
    public void RegisterFailedLogin_locks_the_account_once_the_threshold_is_reached()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime.AddSeconds(attempt));
        }

        var fifthAttemptTime = BaseTime.AddSeconds(4);

        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.True(user.IsLockedOut(fifthAttemptTime));
        Assert.Equal(fifthAttemptTime.AddMinutes(15), user.LockedOutUntil);
    }

    [Fact]
    public void RegisterFailedLogin_resets_the_counter_once_the_sliding_window_has_expired()
    {
        var user = CreateUser();

        user.RegisterFailedLogin(BaseTime);
        user.RegisterFailedLogin(BaseTime.AddMinutes(1));
        user.RegisterFailedLogin(BaseTime.AddMinutes(2));

        // More than 15 minutes after the window started, so this failure restarts the count at 1
        // instead of becoming a 4th accumulated attempt.
        user.RegisterFailedLogin(BaseTime.AddMinutes(20));

        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Equal(BaseTime.AddMinutes(20), user.FailedLoginWindowStartedAt);
        Assert.False(user.IsLockedOut(BaseTime.AddMinutes(20)));
    }

    [Fact]
    public void RegisterFailedLogin_at_exactly_the_window_boundary_increments_the_existing_window()
    {
        var user = CreateUser();

        user.RegisterFailedLogin(BaseTime);

        // Exactly 15 minutes after the window started. The comparison in RegisterFailedLogin is
        // strictly '>', so this instant is still considered inside the window: it must accumulate
        // onto the existing count rather than resetting it.
        user.RegisterFailedLogin(BaseTime.AddMinutes(15));

        Assert.Equal(2, user.FailedLoginAttempts);
        Assert.Equal(BaseTime, user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void RegisterFailedLogin_just_past_the_window_boundary_restarts_the_window()
    {
        var user = CreateUser();

        user.RegisterFailedLogin(BaseTime);

        // One millisecond past the 15-minute boundary: now strictly outside the window, so this
        // failure must restart the count at 1 and move the window start forward.
        var justPastBoundary = BaseTime.AddMinutes(15).AddMilliseconds(1);
        user.RegisterFailedLogin(justPastBoundary);

        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Equal(justPastBoundary, user.FailedLoginWindowStartedAt);
    }

    [Fact]
    public void IsLockedOut_is_true_just_before_expiry_and_false_at_and_after_expiry()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime);
        }

        var lockedOutUntil = user.LockedOutUntil!.Value;

        Assert.True(user.IsLockedOut(lockedOutUntil.AddMilliseconds(-1)));
        Assert.False(user.IsLockedOut(lockedOutUntil));
        Assert.False(user.IsLockedOut(lockedOutUntil.AddMilliseconds(1)));
    }

    [Fact]
    public void RegisterSuccessfulLogin_clears_the_lockout_and_updates_last_login()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime);
        }

        Assert.True(user.IsLockedOut(BaseTime));

        var loginTime = BaseTime.AddMinutes(30);
        user.RegisterSuccessfulLogin(loginTime);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.FailedLoginWindowStartedAt);
        Assert.Null(user.LockedOutUntil);
        Assert.False(user.IsLockedOut(loginTime));
        Assert.Equal(loginTime, user.LastLoginAt);
    }

    private static User CreateUser()
    {
        return new User("jdoe", "jdoe@example.com", "John Doe", "hashed-password");
    }
}
