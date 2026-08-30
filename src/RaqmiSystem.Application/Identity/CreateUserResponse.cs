namespace RaqmiSystem.Application.Identity;

/// <summary>
/// The created account together with its one-time temporary password, mirroring
/// <see cref="ResetPasswordResponse"/>: there is no email/SMTP infrastructure in this repository
/// yet, so this response is the only channel able to deliver the password to the administrator who
/// hands it over. It is generated with a CSPRNG, only its hash is persisted, it is never written to
/// the audit log, and the account is flagged MustChangePassword so it cannot outlive that hand-off.
/// </summary>
public sealed record CreateUserResponse(
    UserAccountDetailResponse User,
    string TemporaryPassword);
