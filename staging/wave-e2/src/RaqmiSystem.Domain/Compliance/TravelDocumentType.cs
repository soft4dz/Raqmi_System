namespace RaqmiSystem.Domain.Compliance;

/// <summary>
/// Identity document shown by the guest for the police record (registre des voyageurs).
/// </summary>
public enum TravelDocumentType
{
    /// <summary>Algerian national identity card.</summary>
    CarteIdentite,

    /// <summary>Passport (the usual document for foreign guests).</summary>
    Passeport,

    /// <summary>Driving licence.</summary>
    PermisConduire,

    /// <summary>Any other identity document (described in the document number field).</summary>
    Autre
}
