namespace RaqmiSystem.Application.Crm;

/// <summary>
/// L'historique des séjours quand aucun module n'en tient : vide, et il l'assume. Un séjour
/// cité n'existe pas, un client n'a jamais séjourné. Ce n'est pas un bouchon de test, c'est le
/// comportement nominal d'une installation sans paquet Hébergement - et celui du CRM tant que
/// l'hôte ne lui a branché aucune source de séjours.
/// </summary>
public sealed class NoStayHistoryReader : IStayHistoryReader
{
    public static NoStayHistoryReader Instance { get; } = new();

    private NoStayHistoryReader()
    {
    }

    public Task<bool> StayExistsAsync(Guid stayId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<GuestStayStatistics> ReadAsync(string customerCode, DateOnly today, CancellationToken cancellationToken)
    {
        return Task.FromResult(GuestStayStatistics.Empty);
    }
}
