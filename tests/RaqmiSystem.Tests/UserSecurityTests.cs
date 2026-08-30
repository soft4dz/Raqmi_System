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

    [Fact]
    public void Unlock_clears_the_lockout_the_failure_counter_and_the_window()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime);
        }

        Assert.True(user.IsLockedOut(BaseTime));

        user.Unlock();

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.FailedLoginWindowStartedAt);
        Assert.Null(user.LockedOutUntil);
        Assert.False(user.IsLockedOut(BaseTime));
    }

    [Fact]
    public void Unlock_does_not_leave_the_account_one_failure_away_from_locking_again()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime);
        }

        user.Unlock();

        // This is why Unlock resets the counter and the window, not only LockedOutUntil: with the
        // counter left at 5, this single failure would immediately re-lock the account and the
        // administrator's intervention would have bought the owner exactly one attempt.
        user.RegisterFailedLogin(BaseTime.AddMinutes(1));

        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(BaseTime.AddMinutes(1)));
    }

    [Fact]
    public void Unlock_is_a_no_op_on_an_account_that_is_not_locked_out_and_preserves_the_last_login()
    {
        var user = CreateUser();
        user.MarkLogin(BaseTime);

        user.Unlock();

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedOutUntil);
        Assert.Equal(BaseTime, user.LastLoginAt);
    }

    [Fact]
    public void SetRoles_revokes_the_absent_ones_adds_the_new_ones_and_keeps_the_kept_ones_intact()
    {
        var user = CreateUser();
        var cashier = new Role("cashier", "Caissier", "Saisie caisse.");
        var reader = new Role("reader", "Lecture seule", "Consultation.");
        var direction = new Role("direction", "Direction", "Pilotage.");

        user.SetRoles([cashier, reader], BaseTime);

        Assert.Equal(2, user.Roles.Count);

        user.SetRoles([reader, direction], BaseTime.AddDays(1));

        Assert.Equal(
            new[] { direction.Id, reader.Id }.Order(),
            user.Roles.Select(userRole => userRole.RoleId).Order());

        // A role that was already held is not removed and re-added: its original assignment date
        // is history, and replacing a set must not rewrite the part that did not change.
        Assert.Equal(BaseTime, user.Roles.Single(userRole => userRole.RoleId == reader.Id).AssignedAt);
        Assert.Equal(BaseTime.AddDays(1), user.Roles.Single(userRole => userRole.RoleId == direction.Id).AssignedAt);
    }

    [Fact]
    public void SetRoles_with_an_empty_set_strips_every_role()
    {
        var user = CreateUser();
        user.SetRoles([new Role("cashier", "Caissier", "Saisie caisse.")], BaseTime);

        user.SetRoles([], BaseTime.AddDays(1));

        Assert.Empty(user.Roles);
    }

    [Fact]
    public void UpdateProfile_renormalizes_the_email_and_leaves_the_sign_in_identifier_alone()
    {
        var user = CreateUser();

        user.UpdateProfile("  Jane.Doe@Example.COM ", "  Jane Doe  ");

        Assert.Equal("Jane.Doe@Example.COM", user.Email);
        Assert.Equal("JANE.DOE@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("Jane Doe", user.DisplayName);

        // The user name is the sign-in identifier: it is not part of the profile update at all.
        Assert.Equal("jdoe", user.UserName);
        Assert.Equal("JDOE", user.NormalizedUserName);
    }

    [Fact]
    public void UpdateProfile_refuses_a_blank_email_or_display_name()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() => user.UpdateProfile("   ", "Jane Doe"));
        Assert.Throws<ArgumentException>(() => user.UpdateProfile("jane@example.com", "   "));
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_the_account_without_touching_the_lockout_state()
    {
        var user = CreateUser();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RegisterFailedLogin(BaseTime);
        }

        user.Deactivate();
        Assert.False(user.IsActive);

        // Reactivation restores access rights, not a clean lockout slate: the two are independent
        // decisions, and an administrator who wants both calls Unlock as well.
        user.Activate();
        Assert.True(user.IsActive);
        Assert.True(user.IsLockedOut(BaseTime));
    }

    private static User CreateUser()
    {
        return new User("jdoe", "jdoe@example.com", "John Doe", "hashed-password");
    }
}
