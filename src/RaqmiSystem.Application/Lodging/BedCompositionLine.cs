namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une ligne de composition de couchage : "2 lits simples", "1 lit king".
/// <paramref name="BedType"/> est le nom d'une valeur de Domain.Lodging.BedType.
/// </summary>
public sealed record BedCompositionLine(string BedType, int Quantity);
