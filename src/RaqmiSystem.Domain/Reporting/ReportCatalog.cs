namespace RaqmiSystem.Domain.Reporting;

/// <summary>
/// The code-defined catalog of the automatic-reports module (same approach as
/// <see cref="Accounting.AccountClassCatalog"/>: a short, stable, verifiable list shipped in
/// code, never data invented at runtime).
///
/// WHAT THIS IS NOT: there is no generic report designer here. Each report below is a thin,
/// typed projection over a module that already exists, and every execution DELEGATES to that
/// module's service so the business rules are applied exactly once, where they live:
/// <list type="bullet">
///   <item>recettes-par-unite counts VALIDATED daily revenue only (revenue module rule);</item>
///   <item>encaissements-par-mode counts CONFIRMED cash receipts only (treasury module rule);</item>
///   <item>balance-agee delegates entirely to the receivables aging service (issued unpaid
///   invoices, aged from the invoice date);</item>
///   <item>tva-facturee reads issued and paid invoices (never drafts, never cancelled ones)
///   through the billing service;</item>
///   <item>occupation-par-unite delegates entirely to the lodging occupancy service.</item>
/// </list>
///
/// Output formats: executions return STRUCTURED data (columns + rows) rendered by a single
/// dynamic grid on the desktop, exportable to CSV client-side. PDF and Excel exports are
/// deliberately OUT OF SCOPE: no PDF/Excel library exists in this repository, and pretending
/// otherwise would be a lie on the module card.
/// </summary>
public static class ReportCatalog
{
    public const string RevenueByUnit = "recettes-par-unite";

    public const string ReceiptsByMethod = "encaissements-par-mode";

    public const string AgedBalance = "balance-agee";

    public const string InvoicedVat = "tva-facturee";

    public const string OccupancyByUnit = "occupation-par-unite";

    /// <summary>Wire keys of the report parameters (ASCII, shared with the desktop client).</summary>
    public const string FromParameter = "from";

    public const string ToParameter = "to";

    public const string AsOfDateParameter = "asOfDate";

    public const string UnitCodeParameter = "unitCode";

    public static IReadOnlyCollection<ReportDefinition> All { get; } = new[]
    {
        new ReportDefinition(
            RevenueByUnit,
            "Recettes par unité",
            "Recettes journalières validées sur la période, agrégées par unité et par catégorie "
                + "(hébergement, restauration, boissons, autres). Les brouillons, les recettes "
                + "soumises et les recettes rejetées sont exclus.",
            new[]
            {
                new ReportParameterDefinition(FromParameter, "Période du", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(ToParameter, "Période au", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(UnitCodeParameter, "Unité (facultatif)", ReportParameterType.HotelUnit, Required: false)
            }),

        new ReportDefinition(
            ReceiptsByMethod,
            "Encaissements par mode de paiement",
            "Encaissements confirmés de la trésorerie sur la période, agrégés par mode de "
                + "paiement. Les encaissements en brouillon et les encaissements annulés sont exclus.",
            new[]
            {
                new ReportParameterDefinition(FromParameter, "Période du", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(ToParameter, "Période au", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(UnitCodeParameter, "Unité (facultatif)", ReportParameterType.HotelUnit, Required: false)
            }),

        new ReportDefinition(
            AgedBalance,
            "Balance âgée des créances",
            "Créances clients par tranche d'ancienneté à la date d'arrêté. Le calcul est délégué "
                + "au module créances : seules les factures émises non réglées sont comptées, "
                + "vieillies depuis leur date de facture.",
            new[]
            {
                new ReportParameterDefinition(AsOfDateParameter, "Date d'arrêté", ReportParameterType.Date, Required: true)
            }),

        new ReportDefinition(
            InvoicedVat,
            "TVA facturée par taux",
            "Factures émises ou payées dont la date de facture est dans la période : base hors "
                + "taxes et TVA collectée par taux (0, 9, 19 %). Les brouillons et les factures "
                + "annulées sont exclus. C'est l'état fiscal de base du chiffre facturé.",
            new[]
            {
                new ReportParameterDefinition(FromParameter, "Période du", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(ToParameter, "Période au", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(UnitCodeParameter, "Unité (facultatif)", ReportParameterType.HotelUnit, Required: false)
            }),

        new ReportDefinition(
            OccupancyByUnit,
            "Occupation par unité",
            "Occupation jour par jour d'une unité sur la période : chambres actives, chambres "
                + "occupées par une réservation couvrant la nuit, et taux d'occupation. Le calcul "
                + "est délégué au module hébergement.",
            new[]
            {
                new ReportParameterDefinition(FromParameter, "Période du", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(ToParameter, "Période au", ReportParameterType.Date, Required: true),
                new ReportParameterDefinition(UnitCodeParameter, "Unité", ReportParameterType.HotelUnit, Required: true)
            })
    };

    /// <summary>
    /// Returns the definition whose code matches (case-insensitively, surrounding spaces
    /// ignored), or null when no report carries that code.
    /// </summary>
    public static ReportDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim();

        return All.SingleOrDefault(definition =>
            string.Equals(definition.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
