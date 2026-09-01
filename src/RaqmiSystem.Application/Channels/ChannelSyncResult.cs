namespace RaqmiSystem.Application.Channels;

/// <summary>Le compte rendu d'un echange avec un canal.</summary>
public sealed record ChannelSyncResult(
    string ProviderCode,
    int SentCount,
    int AcceptedCount,
    int RejectedCount,
    IReadOnlyCollection<string> Messages,
    DateTimeOffset OccurredAt);
