using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Ouvre un folio supplementaire sur un sejour : societe, agence, ou second folio client.
/// <paramref name="BillToCustomerCode"/> nul signifie "le client du sejour".
/// </summary>
public sealed record CreateFolioRequest(
    FolioKind Kind,
    string? BillToCustomerCode = null,
    string? Label = null);
