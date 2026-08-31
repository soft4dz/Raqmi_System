using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// A commercial action run over a period, on one channel, towards one segment of the customer
/// file (or towards every guest when <see cref="TargetSegmentCode"/> is null).
///
/// The whole lifecycle lives HERE rather than in the service, because every transition is a rule
/// about this row alone - the kind of invariant an entity can actually guarantee. The service
/// adds only what needs to see other rows: that the targeted segment exists and is active, and
/// the audience the campaign resolves to.
///
/// EDITABILITY IS THE POINT of the Draft state: from the moment a campaign is scheduled, its
/// message, its channel, its period and its audience are what the guests were told, so
/// <see cref="UpdateDetails"/> refuses to touch anything else. A campaign that must change after
/// that is cancelled and reopened as another one, which leaves both facts in the history.
/// </summary>
public sealed class Campaign : AuditableEntity
{
    private Campaign()
    {
    }

    public Campaign(
        string code,
        string label,
        CampaignChannel channel,
        DateOnly startDate,
        DateOnly endDate,
        string? targetSegmentCode = null,
        string? objective = null,
        string? message = null)
    {
        Code = NormalizeCode(code);
        Status = CampaignStatus.Draft;
        ApplyDetails(label, channel, startDate, endDate, targetSegmentCode, objective, message);
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public CampaignChannel Channel { get; private set; }

    /// <summary>Segment addressed, or null when the campaign addresses the whole customer file.</summary>
    public string? TargetSegmentCode { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public CampaignStatus Status { get; private set; } = CampaignStatus.Draft;

    /// <summary>What the campaign is meant to achieve, in the commercial team's own words.</summary>
    public string? Objective { get; private set; }

    /// <summary>The message served to the audience.</summary>
    public string? Message { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public string? ScheduledBy { get; private set; }

    public DateTimeOffset? LaunchedAt { get; private set; }

    public string? LaunchedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? CompletedBy { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancelledBy { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>A campaign is editable while, and only while, it is still a draft.</summary>
    public bool CanEdit => Status == CampaignStatus.Draft;

    /// <summary>
    /// Does reaching this audience require recorded marketing consent? True for the channels that
    /// PUSH a message at the guest (email, SMS). A phone call from the commercial team and an
    /// offer served at the front desk address a guest the establishment is already dealing with,
    /// so they are not gated on an opt-in the guest was never asked for.
    ///
    /// This is the single source of truth of that rule: the audience query filters on it, and the
    /// screen explains the resulting count with it.
    /// </summary>
    public bool RequiresMarketingConsent => Channel is CampaignChannel.Email or CampaignChannel.Sms;

    /// <summary>Is the campaign live on that day? Used by the 360 view to say what a guest is currently offered.</summary>
    public bool IsLiveOn(DateOnly date)
    {
        return Status == CampaignStatus.Running && date >= StartDate && date <= EndDate;
    }

    public void UpdateDetails(
        string label,
        CampaignChannel channel,
        DateOnly startDate,
        DateOnly endDate,
        string? targetSegmentCode,
        string? objective,
        string? message)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Only a draft campaign can be modified.");
        }

        ApplyDetails(label, channel, startDate, endDate, targetSegmentCode, objective, message);
    }

    /// <summary>Freezes the campaign: it is validated and waiting for its start date.</summary>
    public void Schedule(string userName, DateTimeOffset utcNow)
    {
        if (Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft campaign can be scheduled.");
        }

        Status = CampaignStatus.Scheduled;
        ScheduledAt = utcNow;
        ScheduledBy = RequireActor(userName);
    }

    /// <summary>Puts the campaign live. Only a scheduled campaign can be launched.</summary>
    public void Launch(string userName, DateTimeOffset utcNow)
    {
        if (Status != CampaignStatus.Scheduled)
        {
            throw new InvalidOperationException("Only a scheduled campaign can be launched.");
        }

        Status = CampaignStatus.Running;
        LaunchedAt = utcNow;
        LaunchedBy = RequireActor(userName);
    }

    /// <summary>Closes a campaign that has run its course.</summary>
    public void Complete(string userName, DateTimeOffset utcNow)
    {
        if (Status != CampaignStatus.Running)
        {
            throw new InvalidOperationException("Only a running campaign can be completed.");
        }

        Status = CampaignStatus.Completed;
        CompletedAt = utcNow;
        CompletedBy = RequireActor(userName);
    }

    /// <summary>
    /// Abandons a campaign, with the reason on the record. A completed campaign is history and is
    /// never cancelled: what already reached the guests cannot be unsaid.
    /// </summary>
    public void Cancel(string reason, string userName, DateTimeOffset utcNow)
    {
        if (Status is CampaignStatus.Completed or CampaignStatus.Cancelled)
        {
            throw new InvalidOperationException("A completed or already cancelled campaign cannot be cancelled.");
        }

        var normalizedReason = CrmText.Require(reason, nameof(reason), 500);

        Status = CampaignStatus.Cancelled;
        CancelledAt = utcNow;
        CancelledBy = RequireActor(userName);
        CancelReason = normalizedReason;
    }

    public static string NormalizeCode(string value)
    {
        return CrmText.RequireCode(value, nameof(value), 60);
    }

    private void ApplyDetails(
        string label,
        CampaignChannel channel,
        DateOnly startDate,
        DateOnly endDate,
        string? targetSegmentCode,
        string? objective,
        string? message)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown campaign channel.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException(
                "The end date of a campaign cannot be before its start date.",
                nameof(endDate));
        }

        Label = CrmText.Require(label, nameof(label), 200);
        Channel = channel;
        StartDate = startDate;
        EndDate = endDate;
        TargetSegmentCode = CrmText.OptionalCode(targetSegmentCode, nameof(targetSegmentCode));
        Objective = CrmText.Optional(objective, nameof(objective), 400);
        Message = CrmText.Optional(message, nameof(message), 2000);
    }

    private static string RequireActor(string userName)
    {
        return string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }
}
