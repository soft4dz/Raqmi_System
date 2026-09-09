using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Fiscalite;

public enum VatMovementType { Vente, Avoir }
public enum VatRegisterSource { Manuel, Achats }
public enum DeclarationStatus { Calculee, Exportee, Declaree }
public enum TeleDeclarationType { Tva }
public enum FiscalReturnKind { Simple, Avancee }
public enum SifecTransmissionStatus { Prepare, Soumis, Accepte, Rejete, Erreur }
public enum SifecMode { Sandbox, Production }

/// <summary>
/// One line of the VAT sales register, written by <c>IVatRegisterService.RegisterSaleAsync</c> in
/// the same transaction as <c>BillingService.IssueInvoiceAsync</c> - a chronological legal
/// register, not a report query, so it is a real row rather than a view over invoices.
/// </summary>
public sealed class VatSalesRegisterEntry : AuditableEntity
{
    private VatSalesRegisterEntry() { }

    public VatSalesRegisterEntry(
        Guid invoiceId, string pieceNumber, DateOnly pieceDate, VatMovementType type,
        string customerName, string? customerNif, decimal baseHt, decimal vatAmount)
    {
        if (string.IsNullOrWhiteSpace(pieceNumber)) throw new ArgumentException("Piece number is required.", nameof(pieceNumber));
        if (string.IsNullOrWhiteSpace(customerName)) throw new ArgumentException("Customer name is required.", nameof(customerName));
        if (baseHt < 0) throw new ArgumentOutOfRangeException(nameof(baseHt));
        if (vatAmount < 0) throw new ArgumentOutOfRangeException(nameof(vatAmount));
        InvoiceId = invoiceId; PieceNumber = pieceNumber.Trim(); PieceDate = pieceDate; Type = type;
        CustomerName = customerName.Trim(); CustomerNif = customerNif;
        BaseHt = baseHt; VatAmount = vatAmount;
    }

    public Guid InvoiceId { get; private set; }
    public string PieceNumber { get; private set; } = string.Empty;
    public DateOnly PieceDate { get; private set; }
    public VatMovementType Type { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string? CustomerNif { get; private set; }
    public decimal BaseHt { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Ttc => BaseHt + VatAmount;
}

/// <summary>
/// One line of the VAT purchases register, fed either automatically at purchase order reception
/// (<see cref="VatRegisterSource.Achats"/>, written by <c>IVatRegisterService.RegisterPurchaseAsync</c>
/// from <c>PurchasingService.ReceiveOrderAsync</c>) or by manual entry / batch import of approved
/// orders (<see cref="VatRegisterSource.Manuel"/>).
/// </summary>
public sealed class VatPurchaseRegisterEntry : AuditableEntity
{
    private VatPurchaseRegisterEntry() { }

    public VatPurchaseRegisterEntry(
        Guid? purchaseOrderId, string pieceNumber, DateOnly pieceDate,
        string supplierName, string? supplierNif, decimal baseHt, decimal vatAmount, VatRegisterSource source)
    {
        if (string.IsNullOrWhiteSpace(pieceNumber)) throw new ArgumentException("Piece number is required.", nameof(pieceNumber));
        if (string.IsNullOrWhiteSpace(supplierName)) throw new ArgumentException("Supplier name is required.", nameof(supplierName));
        if (baseHt < 0) throw new ArgumentOutOfRangeException(nameof(baseHt));
        if (vatAmount < 0) throw new ArgumentOutOfRangeException(nameof(vatAmount));
        PurchaseOrderId = purchaseOrderId; PieceNumber = pieceNumber.Trim(); PieceDate = pieceDate;
        SupplierName = supplierName.Trim(); SupplierNif = supplierNif;
        BaseHt = baseHt; VatAmount = vatAmount; Source = source;
    }

    /// <summary>
    /// Null for a manual entry. Set for an entry fed from Achats - also the key
    /// <c>ImportApprovedPurchaseOrdersAsync</c> uses to skip an order already imported, so a
    /// batch import run twice never duplicates a line.
    /// </summary>
    public Guid? PurchaseOrderId { get; private set; }
    public string PieceNumber { get; private set; } = string.Empty;
    public DateOnly PieceDate { get; private set; }
    public string SupplierName { get; private set; } = string.Empty;
    public string? SupplierNif { get; private set; }
    public decimal BaseHt { get; private set; }
    public decimal VatAmount { get; private set; }
    public VatRegisterSource Source { get; private set; }
    public decimal Ttc => BaseHt + VatAmount;
}

/// <summary>
/// Monthly VAT declaration. <see cref="Calculate"/> is a pure aggregation of the two registers for
/// the period plus the prior period's carried-forward credit - it never invents data the registers
/// do not have, matching the legacy rule ("le calcul ne recree pas de donnees manquantes").
/// </summary>
public sealed class VatDeclaration : AuditableEntity
{
    private VatDeclaration() { }

    private VatDeclaration(int year, int month, decimal baseHtVentes, decimal tvaCollectee, decimal tvaDeductible, decimal creditAnterieur)
    {
        Year = year; Month = month;
        BaseHtVentes = baseHtVentes; TvaCollectee = tvaCollectee; TvaDeductible = tvaDeductible; CreditAnterieur = creditAnterieur;
        Solde = tvaCollectee - tvaDeductible - creditAnterieur;
        Status = DeclarationStatus.Calculee;
    }

    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal BaseHtVentes { get; private set; }
    public decimal TvaCollectee { get; private set; }
    public decimal TvaDeductible { get; private set; }

    /// <summary>Absolute value of the previous calculated declaration's negative balance, if any.</summary>
    public decimal CreditAnterieur { get; private set; }

    /// <summary>Positive = due to the DGI; negative = credit carried forward to the next period.</summary>
    public decimal Solde { get; private set; }
    public DeclarationStatus Status { get; private set; }
    public string? DgiReference { get; private set; }

    /// <param name="previousSolde">Solde of the most recent prior calculated declaration, or null if none exists.</param>
    public static VatDeclaration Calculate(
        int year, int month, decimal baseHtVentes, decimal tvaCollectee, decimal tvaDeductible, decimal? previousSolde)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var creditAnterieur = previousSolde is { } solde && solde < 0 ? Math.Abs(solde) : 0m;
        return new VatDeclaration(year, month, baseHtVentes, tvaCollectee, tvaDeductible, creditAnterieur);
    }

    public void MarkExported()
    {
        if (Status == DeclarationStatus.Declaree) throw new InvalidOperationException("Declaration is already declared.");
        Status = DeclarationStatus.Exportee;
    }

    public void MarkDeclared(string dgiReference)
    {
        if (string.IsNullOrWhiteSpace(dgiReference)) throw new ArgumentException("DGI reference is required.", nameof(dgiReference));
        DgiReference = dgiReference.Trim();
        Status = DeclarationStatus.Declaree;
    }
}

/// <summary>
/// A G50-format export prepared for deposit on the DGI portal. The application never deposits it
/// itself - deposit happens outside the application, the user then pastes back the DGI reference
/// via <see cref="MarkDeclared"/>.
/// </summary>
public sealed class TeleDeclaration : AuditableEntity
{
    private TeleDeclaration() { }

    public TeleDeclaration(Guid vatDeclarationId, TeleDeclarationType type, int year, int month, decimal amount)
    {
        VatDeclarationId = vatDeclarationId; Type = type; Year = year; Month = month; Amount = amount;
        Status = DeclarationStatus.Exportee;
        ExportedAt = DateTimeOffset.UtcNow;
    }

    public Guid VatDeclarationId { get; private set; }
    public TeleDeclarationType Type { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal Amount { get; private set; }
    public DeclarationStatus Status { get; private set; }
    public string? DgiReference { get; private set; }
    public DateTimeOffset ExportedAt { get; private set; }

    public void MarkDeclared(string dgiReference)
    {
        if (string.IsNullOrWhiteSpace(dgiReference)) throw new ArgumentException("DGI reference is required.", nameof(dgiReference));
        DgiReference = dgiReference.Trim();
        Status = DeclarationStatus.Declaree;
    }
}

/// <summary>
/// Withholding tax (retenue a la source) on a supplier payment. Rate is an integer percentage
/// (e.g. 15 for 15%), the same convention as <c>InvoiceLine.VatRate</c> - not a 0..1 fraction.
/// </summary>
public sealed class WithholdingTaxEntry : AuditableEntity
{
    public const decimal DefaultRate = 15m;

    private WithholdingTaxEntry() { }

    public WithholdingTaxEntry(string supplierName, decimal baseHt, decimal? rate, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(supplierName)) throw new ArgumentException("Supplier name is required.", nameof(supplierName));
        if (baseHt < 0) throw new ArgumentOutOfRangeException(nameof(baseHt));
        var appliedRate = rate ?? DefaultRate;
        if (appliedRate is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(rate), appliedRate, "Rate must be between 0 and 100.");
        SupplierName = supplierName.Trim(); BaseHt = baseHt; Rate = appliedRate; Date = date;
        MontantRetenu = ComputeAmount(baseHt, appliedRate);
    }

    public string SupplierName { get; private set; } = string.Empty;
    public decimal BaseHt { get; private set; }
    public decimal Rate { get; private set; }
    public decimal MontantRetenu { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>Single source of truth for the calculation: round(baseHt x rate) / 100.</summary>
    public static decimal ComputeAmount(decimal baseHt, decimal rate)
    {
        return Math.Round(baseHt * rate, 2, MidpointRounding.AwayFromZero) / 100m;
    }
}

public sealed class FiscalReturnLine
{
    private FiscalReturnLine() { }

    public FiscalReturnLine(string code, string label, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label is required.", nameof(label));
        Code = code.Trim(); Label = label.Trim(); Amount = amount;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FiscalReturnId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
}

/// <summary>
/// Annual fiscal return ("liasse fiscale"). <see cref="GenerateSimple"/> and
/// <see cref="GenerateAdvanced"/> are pure functions over the year's totals - they never read the
/// registers themselves, the caller (service layer) aggregates first. The advanced IBS figure is
/// explicitly a simplified estimate, never an official tax liquidation - see the field doc comment.
/// </summary>
public sealed class FiscalReturn : AuditableEntity
{
    /// <summary>Corporate tax rate used ONLY to estimate IBS in the advanced return - not an official liquidation.</summary>
    public const decimal EstimatedIbsRate = 0.26m;

    private readonly List<FiscalReturnLine> _lines = new();
    private FiscalReturn() { }

    private FiscalReturn(int year, FiscalReturnKind kind, IEnumerable<FiscalReturnLine> lines)
    {
        Year = year; Kind = kind; GeneratedAt = DateTimeOffset.UtcNow;
        _lines.AddRange(lines);
    }

    public int Year { get; private set; }
    public FiscalReturnKind Kind { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
    public IReadOnlyCollection<FiscalReturnLine> Lines => _lines.AsReadOnly();

    /// <summary>3 lines from the sales register alone: CA HT, TVA collectee, resultat simplifie = CA HT x 0.15.</summary>
    public static FiscalReturn GenerateSimple(int year, decimal caHt, decimal tvaCollectee)
    {
        var resultatSimplifie = Math.Round(caHt * 0.15m, 2, MidpointRounding.AwayFromZero);
        return new FiscalReturn(year, FiscalReturnKind.Simple, new[]
        {
            new FiscalReturnLine("G50-001", "Chiffre d'affaires HT", caHt),
            new FiscalReturnLine("G50-010", "TVA collectee", tvaCollectee),
            new FiscalReturnLine("G29-001", "Resultat comptable simplifie", resultatSimplifie),
        });
    }

    /// <summary>9 lines crossing sales and purchases for the year. IBS is an estimate, not a liquidation.</summary>
    public static FiscalReturn GenerateAdvanced(
        int year, decimal caHt, decimal achatsHt, decimal tvaCollectee, decimal tvaDeductible, decimal creditTvaAnterieurCumule)
    {
        var soldeTva = tvaCollectee - tvaDeductible - creditTvaAnterieurCumule;
        var resultatFiscal = caHt - achatsHt;
        var ibsEstime = Math.Max(0m, Math.Round(resultatFiscal * EstimatedIbsRate, 2, MidpointRounding.AwayFromZero));
        return new FiscalReturn(year, FiscalReturnKind.Avancee, new[]
        {
            new FiscalReturnLine("G50-001", "Chiffre d'affaires HT", caHt),
            new FiscalReturnLine("G50-002", "Achats et charges HT", achatsHt),
            new FiscalReturnLine("G50-010", "TVA collectee", tvaCollectee),
            new FiscalReturnLine("G50-011", "TVA deductible", tvaDeductible),
            new FiscalReturnLine("G50-012", "Credit de TVA anterieur", creditTvaAnterieurCumule),
            new FiscalReturnLine("G50-013", "Solde TVA a payer", soldeTva),
            new FiscalReturnLine("G4-001", "Resultat fiscal simplifie", resultatFiscal),
            new FiscalReturnLine("G29-001", "IBS estime (26%, simplifie)", ibsEstime),
            new FiscalReturnLine("G29-002", "Resultat net estime", resultatFiscal - ibsEstime),
        });
    }
}

/// <summary>
/// One SIFEC transmission attempt for an issued invoice. Sandbox mode always simulates acceptance
/// (certification/testing); production mode has no real DGI integration and must always fail
/// explicitly - see <c>SifecService</c> - never report a simulated success as if it were real.
/// </summary>
public sealed class SifecTransmission : AuditableEntity
{
    private SifecTransmission() { }

    public SifecTransmission(Guid invoiceId, string payloadHash, string qrPayload, SifecMode mode)
    {
        if (string.IsNullOrWhiteSpace(payloadHash)) throw new ArgumentException("Payload hash is required.", nameof(payloadHash));
        if (string.IsNullOrWhiteSpace(qrPayload)) throw new ArgumentException("QR payload is required.", nameof(qrPayload));
        InvoiceId = invoiceId; PayloadHash = payloadHash; QrPayload = qrPayload; Mode = mode;
        Status = SifecTransmissionStatus.Prepare;
    }

    public Guid InvoiceId { get; private set; }
    public SifecTransmissionStatus Status { get; private set; }
    public string? Uid { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public string QrPayload { get; private set; } = string.Empty;
    public SifecMode Mode { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? ResponseMessage { get; private set; }

    public void MarkAccepted(string uid, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("UID is required.", nameof(uid));
        Uid = uid.Trim(); Status = SifecTransmissionStatus.Accepte; SubmittedAt = now; ResponseMessage = null;
    }

    public void MarkRejected(string message, DateTimeOffset now)
    {
        Status = SifecTransmissionStatus.Rejete; SubmittedAt = now;
        ResponseMessage = string.IsNullOrWhiteSpace(message) ? "Rejete par la DGI." : message.Trim();
    }

    public void MarkError(string message, DateTimeOffset now)
    {
        Status = SifecTransmissionStatus.Erreur; SubmittedAt = now;
        ResponseMessage = string.IsNullOrWhiteSpace(message) ? "Erreur de transmission." : message.Trim();
    }

    public void MarkSubmitted(DateTimeOffset now)
    {
        Status = SifecTransmissionStatus.Soumis; SubmittedAt = now;
    }
}
