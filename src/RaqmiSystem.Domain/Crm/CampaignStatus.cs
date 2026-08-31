namespace RaqmiSystem.Domain.Crm;

/// <summary>
/// Where a campaign stands. The lifecycle is Draft -> Scheduled -> Running -> Completed, with
/// <see cref="Cancelled"/> reachable from any state that is not already terminal. Only a
/// <see cref="Draft"/> can still be edited: from the moment a campaign is scheduled, what it says
/// and who it addresses is what the audience was told.
/// </summary>
public enum CampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Running = 2,
    Completed = 3,
    Cancelled = 4
}
