namespace RaqmiSystem.Application.Mice;

/// <summary>Annule un bloc. Le motif est obligatoire : un groupe perdu se justifie.</summary>
public sealed record CancelRoomAllotmentRequest(string Reason);
