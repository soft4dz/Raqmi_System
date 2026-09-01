namespace RaqmiSystem.Application.Lodging;

/// <summary>Remboursement ou conservation d'un acompte : les deux exigent un motif ecrit.</summary>
public sealed record CloseDepositRequest(string Reason, DateOnly? Date = null);
