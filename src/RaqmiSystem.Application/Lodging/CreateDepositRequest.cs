namespace RaqmiSystem.Application.Lodging;

public sealed record CreateDepositRequest(decimal Amount, DateOnly DueDate, string? Notes = null);
