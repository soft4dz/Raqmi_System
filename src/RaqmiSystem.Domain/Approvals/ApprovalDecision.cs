namespace RaqmiSystem.Domain.Approvals;

/// <summary>
/// The recorded decision on one step of an approval instance: who decided, in which direction,
/// when, and why (the comment is MANDATORY on a rejection - a refusal without a stated reason
/// is not auditable). Decisions are only ever appended by <see cref="ApprovalInstance.Decide"/>,
/// never edited: they are the audit trail of the workflow.
/// </summary>
public sealed class ApprovalDecision
{
    private ApprovalDecision()
    {
    }

    internal ApprovalDecision(
        int rank,
        string stepLabel,
        string decidedBy,
        bool approved,
        string? comment,
        DateTimeOffset decidedAt)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be at least 1.");
        }

        var normalizedComment = NormalizeComment(comment);

        if (!approved && normalizedComment is null)
        {
            throw new ArgumentException("A rejection requires a comment stating the reason.", nameof(comment));
        }

        Rank = rank;
        StepLabel = stepLabel;
        DecidedBy = RequireActor(decidedBy);
        Approved = approved;
        Comment = normalizedComment;
        DecidedAt = decidedAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InstanceId { get; private set; }

    public int Rank { get; private set; }

    public string StepLabel { get; private set; } = string.Empty;

    public string DecidedBy { get; private set; } = string.Empty;

    public bool Approved { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();

        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Comment cannot exceed 500 characters.", nameof(comment));
        }

        return trimmed;
    }

    private static string RequireActor(string userName)
    {
        return string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }
}
