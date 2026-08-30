namespace RaqmiSystem.Application.Identity;

/// <summary>
/// The single place stating how long a password chosen by a human must be.
///
/// The threshold is not new: <c>SecuritySeeder.SeedInitialAdminAsync</c> already refuses to seed the
/// initial administrator with a password shorter than 12 characters. It is restated here as a
/// constant rather than re-typed as a second literal, so the self-service change and the seeder can
/// never drift into disagreeing about what an acceptable password is.
///
/// It deliberately applies only to passwords a person chooses. The server-generated temporary
/// passwords (<c>TemporaryPasswordGenerator</c>, used by account creation and by the administrative
/// reset) are held to their own, stronger, generator-side guarantee.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>
    /// Minimum length of a user-chosen password, in characters. Same value the initial-administrator
    /// seeding has always enforced.
    /// </summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// Upper bound on a user-chosen password. Nothing about PBKDF2 needs it - it exists so an
    /// unbounded string from the network never reaches the key-derivation function, and so the
    /// stored hash stays a predictable size. Far above any realistic passphrase.
    /// </summary>
    public const int MaximumLength = 256;
}
