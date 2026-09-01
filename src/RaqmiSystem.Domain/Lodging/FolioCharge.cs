namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une ligne de <see cref="Folio"/>. Modelisee en entite fille avec sa propre table et une cle
/// etrangere obligatoire (meme raisonnement que InvoiceLine) : configuration snake_case explicite,
/// contraintes nommees, et un identifiant stable que les reponses d'API peuvent referencer.
///
/// REGLE DE SIGNE : un montant NEGATIF n'est legitime que sur un <see cref="ChargeKind.Settlement"/>
/// (un reglement impute au folio) ou un <see cref="ChargeKind.Adjustment"/> (geste commercial) ;
/// une nuit, un extra, une taxe ou une composante de forfait se facturent toujours positivement.
/// Zero n'est jamais une ligne.
///
/// MONTANT TOUTES TAXES COMPRISES. <see cref="Amount"/> est ce que le client doit : c'est lui qui
/// fait le solde, et c'est le solde qui autorise le depart. La TVA est portee a cote
/// (<see cref="VatRate"/>) et EXTRAITE du montant quand la ligne part en facture - l'inverse
/// obligerait le comptoir a annoncer un solde hors taxes, que personne ne paie.
///
/// IDEMPOTENCE DU POSTING. <see cref="SourceReference"/> porte la cle du geste qui a produit la
/// ligne ("night:{id}:{date}", "eci:{id}", ...). Elle est unique dans le folio, ce qui est le
/// mecanisme qui empeche un night audit relance de doubler les nuitees : la seconde insertion
/// heurte l'index unique au lieu de passer.
/// </summary>
public sealed class FolioCharge
{
    public const int LabelMaxLength = 300;
    public const int ReferenceMaxLength = 100;
    public const int SourceReferenceMaxLength = 120;

    private FolioCharge()
    {
    }

    public FolioCharge(
        DateOnly chargeDate,
        string label,
        decimal amount,
        ChargeKind kind,
        string? reference = null,
        decimal quantity = 1m,
        decimal? vatRate = null,
        string? extraCode = null,
        string? sourceReference = null,
        DateOnly? businessDate = null)
    {
        ChargeDate = chargeDate;
        Label = RequireValue(label, nameof(label), LabelMaxLength);
        Kind = kind;
        Amount = RequireAmount(amount, kind);
        Reference = NormalizeReference(reference);
        Quantity = RequireQuantity(quantity);
        VatRate = vatRate is { } rate
            ? RaqmiSystem.Domain.Billing.InvoiceLine.RequireAllowedVatRate(rate, nameof(vatRate))
            : null;
        ExtraCode = LodgingText.OptionalCode(extraCode, nameof(extraCode));
        SourceReference = LodgingText.Optional(sourceReference, nameof(sourceReference), SourceReferenceMaxLength);
        BusinessDate = businessDate;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid FolioId { get; private set; }

    public int LineNumber { get; private set; }

    /// <summary>Date a laquelle la prestation a ete consommee.</summary>
    public DateOnly ChargeDate { get; private set; }

    /// <summary>
    /// Journee d'exploitation a laquelle la ligne est rattachee (voir <see cref="BusinessDay"/>).
    /// Nulle sur les lignes anterieures a la date metier ; elles restent rattachees a leur
    /// <see cref="ChargeDate"/>, ce qui est le comportement qu'elles ont toujours eu.
    /// </summary>
    public DateOnly? BusinessDate { get; private set; }

    public string Label { get; private set; } = string.Empty;

    /// <summary>Montant TTC de la ligne. C'est lui qui fait le solde du folio.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Quantite facturee. Un pour la plupart des lignes ; utile aux extras et aux minibars.</summary>
    public decimal Quantity { get; private set; } = 1m;

    /// <summary>Taux de TVA de la ligne (0, 9 ou 19). Null quand il n'est pas renseigne.</summary>
    public decimal? VatRate { get; private set; }

    public ChargeKind Kind { get; private set; }

    /// <summary>Code de l'extra du referentiel a l'origine de la ligne, quand il y en a un.</summary>
    public string? ExtraCode { get; private set; }

    /// <summary>
    /// Reference libre, typiquement le numero de piece de tresorerie que reflete une ligne de
    /// reglement.
    /// </summary>
    public string? Reference { get; private set; }

    /// <summary>
    /// Cle du geste qui a produit la ligne, unique dans le folio. C'est elle qui rend le posting
    /// idempotent : rejouer le meme geste ne peut pas creer une seconde ligne.
    /// </summary>
    public string? SourceReference { get; private set; }

    /// <summary>Montant hors taxes, extrait du montant TTC. Egal au TTC quand aucun taux n'est connu.</summary>
    public decimal AmountExclVat => VatRate is { } rate && rate > 0
        ? Math.Round(Amount / (1m + (rate / 100m)), 2, MidpointRounding.AwayFromZero)
        : Amount;

    /// <summary>Part de TVA contenue dans le montant TTC.</summary>
    public decimal VatAmount => Amount - AmountExclVat;

    internal void SetLineNumber(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    private static decimal RequireAmount(decimal value, ChargeKind kind)
    {
        if (value == 0)
        {
            throw new ArgumentException("Une ligne de folio ne peut pas porter un montant nul.", nameof(value));
        }

        if (decimal.Round(value, 2) != value)
        {
            throw new ArgumentException("Le montant ne peut pas porter plus de deux decimales.", nameof(value));
        }

        if (value < 0 && kind is not (ChargeKind.Settlement or ChargeKind.Adjustment))
        {
            throw new ArgumentException(
                "Seules les lignes de reglement ou d'ajustement peuvent porter un montant negatif.",
                nameof(value));
        }

        return value;
    }

    private static decimal RequireQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "La quantite doit etre strictement positive.");
        }

        if (decimal.Round(quantity, 3) != quantity)
        {
            throw new ArgumentException("La quantite ne peut pas porter plus de trois decimales.", nameof(quantity));
        }

        return quantity;
    }

    private static string? NormalizeReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();

        if (trimmed.Length > ReferenceMaxLength)
        {
            throw new ArgumentException(
                $"La valeur ne peut pas depasser {ReferenceMaxLength} caracteres.",
                nameof(reference));
        }

        return trimmed;
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est requise.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"La valeur ne peut pas depasser {maxLength} caracteres.", argumentName);
        }

        return trimmed;
    }
}
