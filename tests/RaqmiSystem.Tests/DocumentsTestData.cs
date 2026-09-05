using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Settings;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Tests;

/// <summary>
/// Jeu de donnees de la chaine documentaire : une facture emise a TROIS taux de TVA (0, 9 et 19 %),
/// le client societe qui la recoit, l'etablissement qui l'emet. Les montants de lignes sont
/// calcules ici comme le serveur le fait (arrondi commercial a 2 decimales) ; les totaux de la
/// facture sont surchargeables pour prouver que le document les recopie sans les recalculer.
/// </summary>
internal static class DocumentsTestData
{
    public const string IssuerNif = "098765432112345";

    public static InvoiceLineResponse Line(int number, string designation, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        var lineTotalExclVat = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(lineTotalExclVat * vatRate / 100m, 2, MidpointRounding.AwayFromZero);

        return new InvoiceLineResponse(
            Guid.NewGuid(),
            number,
            designation,
            quantity,
            unitPrice,
            vatRate,
            lineTotalExclVat,
            vatAmount,
            lineTotalExclVat + vatAmount);
    }

    public static IReadOnlyCollection<InvoiceLineResponse> ThreeVatRateLines()
    {
        return
        [
            Line(1, "Taxe de séjour", 2m, 200m, 0m),
            Line(2, "Hébergement chambre double", 2m, 12_500.00m, 9m),
            Line(3, "Restauration", 3m, 1_850.50m, 19m)
        ];
    }

    public static InvoiceResponse IssuedInvoice(
        IReadOnlyCollection<InvoiceLineResponse>? lines = null,
        decimal? totalExclVat = null,
        decimal? totalVat = null,
        decimal? totalInclVat = null,
        string? number = "FAC-2026-000042",
        InvoiceStatus status = InvoiceStatus.Issued,
        string? customerName = "Sonatrach Spa")
    {
        lines ??= ThreeVatRateLines();

        var exclVat = totalExclVat ?? lines.Sum(line => line.LineTotalExclVat);
        var vat = totalVat ?? lines.Sum(line => line.VatAmount);
        var inclVat = totalInclVat ?? exclVat + vat;

        return new InvoiceResponse(
            Id: Guid.NewGuid(),
            Number: number,
            CustomerCode: "SONATRACH",
            CustomerName: customerName,
            HotelUnitCode: "ALG01",
            InvoiceDate: new DateOnly(2026, 3, 10),
            Status: status,
            TotalExclVat: exclVat,
            TotalVat: vat,
            TotalInclVat: inclVat,
            Lines: lines,
            CanEdit: status == InvoiceStatus.Draft,
            IssuedAt: status == InvoiceStatus.Draft ? null : new DateTimeOffset(2026, 3, 10, 9, 30, 0, TimeSpan.Zero),
            IssuedBy: status == InvoiceStatus.Draft ? null : "billing.issuer",
            PaidAt: null,
            PaidBy: null,
            CancelledAt: null,
            CancelledBy: null,
            CancellationReason: null,
            IssuerName: status == InvoiceStatus.Draft ? null : "Hotel El Manar Spa",
            IssuerNif: status == InvoiceStatus.Draft ? null : IssuerNif,
            IssuerRc: status == InvoiceStatus.Draft ? null : "16/00-1234567B99",
            IssuerAi: status == InvoiceStatus.Draft ? null : "16012345678",
            IssuerNis: status == InvoiceStatus.Draft ? null : "543211234509876",
            IssuerAddress: status == InvoiceStatus.Draft ? null : "Boulevard des Martyrs",
            CreatedAt: new DateTimeOffset(2026, 3, 9, 16, 0, 0, TimeSpan.Zero),
            CreatedBy: "billing.writer",
            UpdatedAt: null,
            UpdatedBy: null);
    }

    public static CustomerResponse CompanyCustomer()
    {
        return new CustomerResponse(
            Guid.NewGuid(),
            "SONATRACH",
            "Sonatrach Spa",
            CustomerType.Company,
            "099912345678901",
            "16/00-9876543A11",
            "16098765432",
            "123456789012345",
            "Djenane El Malik, Hydra",
            "Alger",
            "+213 21 54 70 00",
            "facturation@sonatrach.dz",
            true,
            DateTimeOffset.UtcNow,
            "tests",
            null,
            null);
    }

    public static CustomerResponse IndividualCustomer()
    {
        return new CustomerResponse(
            Guid.NewGuid(),
            "DUPONT",
            "Karim Benali",
            CustomerType.Individual,
            "111111111111111",
            "16/00-0000001B00",
            "16000000001",
            "000000000000001",
            "12 rue Didouche Mourad",
            "Alger",
            null,
            null,
            true,
            DateTimeOffset.UtcNow,
            "tests",
            null,
            null);
    }

    public static ApplicationSettingsResponse Settings(string? companyNif = IssuerNif)
    {
        return new ApplicationSettingsResponse(
            "Hotel El Manar Spa",
            companyNif,
            "16/00-1234567B99",
            "16012345678",
            "543211234509876",
            "Boulevard des Martyrs",
            "Alger",
            "+213 21 00 00 00",
            "contact@elmanar.dz",
            19m,
            "DZD",
            365,
            true,
            DateTimeOffset.UtcNow,
            "tests",
            null,
            null);
    }
}
