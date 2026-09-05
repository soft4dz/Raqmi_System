namespace RaqmiSystem.Application.Documents;

/// <summary>
/// Une partie d'un document commercial - l'emetteur ou le destinataire - telle que le gabarit
/// l'imprime. Les identifiants fiscaux sont ceux de l'Algerie (NIF, RC, AI, NIS) ; le gabarit
/// n'imprime que ceux qui sont renseignes.
/// </summary>
public sealed record DocumentPartyModel(
    string Name,
    string? Address,
    string? City,
    string? Nif,
    string? Rc,
    string? Ai,
    string? Nis,
    string? Phone,
    string? Email);
