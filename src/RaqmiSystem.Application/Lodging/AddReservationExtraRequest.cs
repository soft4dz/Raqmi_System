namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Attache un extra du referentiel a un sejour. Le prix et le taux de TVA sont FIGES au moment de
/// l'attachement : une hausse ulterieure du tarif du petit-dejeuner ne doit pas reecrire ce qui a
/// ete promis a la vente.
/// </summary>
public sealed record AddReservationExtraRequest(
    string ExtraCode,
    decimal Quantity = 1m,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Notes = null);
