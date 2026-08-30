using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.Receivables;
using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Desktop.Api;

// Module Creances & recouvrement : appels du groupe /api/v1/receivables.
// Fichier de classe partielle, pour que ce chantier n'entre pas en conflit avec
// les autres modules qui alimentent le meme client API.
//
// Le module ne cree aucune donnee financiere : la balance agee et le risque
// client sont recalcules par le serveur a chaque appel, et la seule ecriture est
// l'enregistrement de la trace d'une relance DEJA effectuee par un agent. Le
// serveur n'envoie rien au client final, et ce client API non plus.
public sealed partial class RaqmiApiClient
{
    private const string ReceivablesAgingPath = "/api/v1/receivables/aging";
    private const string ReceivablesRemindersPath = "/api/v1/receivables/reminders";
    private const string ReceivablesCustomersPath = "/api/v1/receivables/customers";

    /// <summary>
    /// Balance agee des creances a la date d'arrete demandee. Sans date, le serveur
    /// retient sa propre date du jour. Le perimetre retenu et la base d'anciennete
    /// voyagent avec les chiffres (proprietes Scope et AgingBasis de la reponse) :
    /// ils sont a afficher tels quels, jamais reformules par le poste.
    /// </summary>
    public async Task<AgingBalanceResponse> GetAgingBalanceAsync(
        string apiBaseUrl,
        DateOnly? asOfDate,
        string? customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildReceivablesAgingQuery(asOfDate, customerCode),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<AgingBalanceResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Historique des relances consignees, filtre cote serveur. Tous les criteres
    /// sont facultatifs.
    /// </summary>
    public async Task<IReadOnlyCollection<ReminderResponse>> GetRemindersAsync(
        string apiBaseUrl,
        string? customerCode,
        string? invoiceNumber,
        DateOnly? from,
        DateOnly? to,
        ReminderLevel? level,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            BuildReceivablesRemindersQuery(customerCode, invoiceNumber, from, to, level),
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<ReminderResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Consigne une relance deja effectuee par un agent. Rien n'est envoye au
    /// client final : cet appel n'ecrit qu'une trace, avec son auteur et sa date.
    /// </summary>
    public async Task<ReminderResponse> CreateReminderAsync(
        string apiBaseUrl,
        CreateReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Post,
            ReceivablesRemindersPath,
            request,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<ReminderResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Instantane du risque porte par un client : encours, facture la plus ancienne
    /// non payee et trace des relances. Calcule par le serveur a sa propre date du
    /// jour, qu'il renvoie dans AsOfDate.
    /// </summary>
    public async Task<CustomerRiskResponse> GetCustomerRiskAsync(
        string apiBaseUrl,
        string customerCode,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(
            apiBaseUrl,
            HttpMethod.Get,
            $"{ReceivablesCustomersPath}/{Uri.EscapeDataString(customerCode)}/risk",
            null,
            includeAuthorization: true,
            cancellationToken);

        return await ReadResponseAsync<CustomerRiskResponse>(response, cancellationToken);
    }

    private static string BuildReceivablesAgingQuery(DateOnly? asOfDate, string? customerCode)
    {
        var query = new List<string>();

        AppendReceivablesDate(query, "asOfDate", asOfDate);
        AppendReceivablesText(query, "customerCode", customerCode);

        return ComposeReceivablesQuery(ReceivablesAgingPath, query);
    }

    private static string BuildReceivablesRemindersQuery(
        string? customerCode,
        string? invoiceNumber,
        DateOnly? from,
        DateOnly? to,
        ReminderLevel? level)
    {
        var query = new List<string>();

        AppendReceivablesText(query, "customerCode", customerCode);
        AppendReceivablesText(query, "invoiceNumber", invoiceNumber);
        AppendReceivablesDate(query, "from", from);
        AppendReceivablesDate(query, "to", to);

        if (level.HasValue)
        {
            // Le serveur analyse le niveau sans tenir compte de la casse : le nom
            // du membre d'enumeration est donc envoye tel quel.
            query.Add("level=" + Uri.EscapeDataString(level.Value.ToString()));
        }

        return ComposeReceivablesQuery(ReceivablesRemindersPath, query);
    }

    // Les dates partent toujours en ISO (yyyy-MM-dd), independamment de la culture
    // du poste : c'est le format que lie le DateOnly cote serveur.
    private static void AppendReceivablesDate(List<string> query, string name, DateOnly? value)
    {
        if (value.HasValue)
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
    }

    private static void AppendReceivablesText(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }
    }

    private static string ComposeReceivablesQuery(string basePath, List<string> query)
    {
        return query.Count == 0 ? basePath : basePath + "?" + string.Join("&", query);
    }
}
