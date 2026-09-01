using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Tariffs;

/// <summary>
/// A pricing plan owned by one hotel unit. Each unit designates at most ONE default active plan
/// (the plan nightly-rate resolution falls back to when no customer convention applies); the
/// invariant is guaranteed by the filtered unique index ux_rate_plans_default_per_unit (see
/// <c>RatePlanConfiguration</c>), which only constrains rows where is_default AND is_active.
/// </summary>
public sealed class RatePlan : AuditableEntity
{
    private RatePlan()
    {
    }

    public RatePlan(string code, string label, string hotelUnitCode, bool isDefault = false)
    {
        Code = NormalizeCode(code);
        Label = RequireValue(label, nameof(label), 160);
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        IsDefault = isDefault;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string HotelUnitCode { get; private set; } = string.Empty;

    /// <summary>
    /// True when this plan is THE default plan of its unit. Only meaningful together with
    /// <see cref="IsActive"/>: an inactive plan keeps its flag as dormant history, but the
    /// filtered unique index only enforces uniqueness among ACTIVE defaults, and resolution
    /// only ever selects an active default.
    /// </summary>
    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    // ------------------------- Conditions commerciales du plan (PMS) -------------------------

    /// <summary>
    /// Devise du plan, code ISO a trois lettres. "DZD" par defaut : un plan sans devise declaree
    /// se lit dans la monnaie de l'etablissement, ce qui est le cas courant.
    /// </summary>
    public string CurrencyCode { get; private set; } = "DZD";

    /// <summary>
    /// Pension comprise. C'est elle qui commande le posting automatique de la pension par le night
    /// audit : sans elle, une demi-pension vendue ne produirait aucune ligne et le restaurant
    /// travaillerait a l'aveugle.
    /// </summary>
    public BoardType Board { get; private set; } = BoardType.RoomOnly;

    /// <summary>
    /// Politique d'annulation imposee par le plan. Figee dans chaque dossier au moment de la
    /// confirmation ; la changer ici n'affecte que les ventes futures.
    /// </summary>
    public string? CancellationPolicyCode { get; private set; }

    /// <summary>
    /// Garantie exigee pour vendre sur ce plan. <see cref="GuaranteeKind.None"/> signifie qu'aucune
    /// garantie n'est demandee - c'est le cas d'un tarif public standard.
    /// </summary>
    public GuaranteeKind RequiredGuarantee { get; private set; } = GuaranteeKind.None;

    /// <summary>Acompte exige, en % du sejour. Zero quand le plan n'en demande pas.</summary>
    public decimal DepositPercent { get; private set; }

    /// <summary>
    /// Faux pour un tarif non remboursable. Distinct de la politique d'annulation : celle-ci decrit
    /// le bareme, celui-la est l'etiquette commerciale que le client voit.
    /// </summary>
    public bool IsRefundable { get; private set; } = true;

    /// <summary>Segment de marche auquel le plan est reserve. Null = tous les segments.</summary>
    public string? MarketSegmentCode { get; private set; }

    /// <summary>Canal de distribution auquel le plan est reserve. Null = tous les canaux.</summary>
    public string? ChannelCode { get; private set; }

    /// <summary>Premiere date de SEJOUR couverte par le plan. Null = pas de borne.</summary>
    public DateOnly? ValidFrom { get; private set; }

    /// <summary>Derniere date de SEJOUR couverte, incluse. Null = pas de borne.</summary>
    public DateOnly? ValidTo { get; private set; }

    public int DisplayOrder { get; private set; }

    public void UpdateDetails(string label)
    {
        Label = RequireValue(label, nameof(label), 160);
    }

    /// <summary>Declare les conditions commerciales du plan.</summary>
    public void SetTerms(
        string currencyCode,
        BoardType board,
        string? cancellationPolicyCode,
        GuaranteeKind requiredGuarantee,
        decimal depositPercent,
        bool isRefundable)
    {
        if (!Enum.IsDefined(board))
        {
            throw new ArgumentOutOfRangeException(nameof(board), board, "Pension inconnue.");
        }

        if (!Enum.IsDefined(requiredGuarantee))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredGuarantee),
                requiredGuarantee,
                "Nature de garantie inconnue.");
        }

        if (depositPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depositPercent),
                depositPercent,
                "L'acompte exige doit etre compris entre 0 et 100 %.");
        }

        if (decimal.Round(depositPercent, 2) != depositPercent)
        {
            throw new ArgumentException(
                "L'acompte exige ne peut pas porter plus de deux decimales.",
                nameof(depositPercent));
        }

        CurrencyCode = RequireValue(currencyCode, nameof(currencyCode), 3).ToUpperInvariant();
        Board = board;
        CancellationPolicyCode = NormalizeOptionalCode(cancellationPolicyCode, nameof(cancellationPolicyCode));
        RequiredGuarantee = requiredGuarantee;
        DepositPercent = depositPercent;
        IsRefundable = isRefundable;
    }

    /// <summary>Restreint la distribution du plan et sa fenetre de validite.</summary>
    public void SetDistribution(
        string? marketSegmentCode,
        string? channelCode,
        DateOnly? validFrom,
        DateOnly? validTo,
        int displayOrder)
    {
        if (validFrom is { } from && validTo is { } to && to < from)
        {
            throw new ArgumentException(
                "La date de fin de validite ne peut pas preceder la date de debut.",
                nameof(validTo));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                displayOrder,
                "L'ordre d'affichage ne peut pas etre negatif.");
        }

        MarketSegmentCode = NormalizeOptionalCode(marketSegmentCode, nameof(marketSegmentCode));
        ChannelCode = NormalizeOptionalCode(channelCode, nameof(channelCode));
        ValidFrom = validFrom;
        ValidTo = validTo;
        DisplayOrder = displayOrder;
    }

    /// <summary>Le plan est-il vendable pour un sejour commencant a cette date, sur ce canal ?</summary>
    public bool IsSellable(DateOnly arrival, string? channelCode = null, string? marketSegmentCode = null)
    {
        if (!IsActive)
        {
            return false;
        }

        if (ValidFrom is { } from && arrival < from)
        {
            return false;
        }

        if (ValidTo is { } to && arrival > to)
        {
            return false;
        }

        if (ChannelCode is not null
            && !string.Equals(ChannelCode, channelCode?.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            return false;
        }

        return MarketSegmentCode is null
            || string.Equals(
                MarketSegmentCode,
                marketSegmentCode?.Trim().ToUpperInvariant(),
                StringComparison.Ordinal);
    }

    private static string? NormalizeOptionalCode(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequireValue(value, argumentName, 40).ToUpperInvariant();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
