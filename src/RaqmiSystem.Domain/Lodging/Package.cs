using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Un forfait : une chambre et des prestations vendues sous un PRIX GLOBAL unique, avec une
/// ventilation interne qui dit ce que chaque service a reellement produit.
///
/// LA VENTILATION EST LE COEUR DU SUJET, PAS LE PRIX. Un "week-end en amoureux" a 25 000 DA n'est
/// pas 25 000 DA d'hebergement : c'est 16 000 de chambre, 3 000 de petit-dejeuner, 4 500 de diner
/// et 1 500 de spa. Sans ventilation, la restauration et le spa ne voient jamais leur chiffre, le
/// RevPAR est faux par exces, et le controle de gestion ne peut pas dire si le forfait est
/// rentable. C'est exactement l'erreur que ce modele refuse de laisser faire : la somme des
/// composantes doit egaler le prix global.
///
/// LE FORFAIT NE REMPLACE PAS LE PLAN TARIFAIRE. Il s'y adosse : le plan dit quand et a qui il se
/// vend, le forfait dit ce qu'il contient.
/// </summary>
public sealed class Package : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int LabelMaxLength = 160;
    public const int DescriptionMaxLength = 1000;

    private readonly List<PackageComponent> components = [];

    private Package()
    {
    }

    public Package(
        string hotelUnitCode,
        string code,
        string label,
        decimal totalPrice,
        string? description = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = LodgingText.RequireCode(code, nameof(code), CodeMaxLength);
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
        TotalPrice = LodgingText.Money(totalPrice, nameof(totalPrice));
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Prix global TTC annonce au client.</summary>
    public decimal TotalPrice { get; private set; }

    /// <summary>Plan tarifaire porteur du forfait, quand il en existe un.</summary>
    public string? RatePlanCode { get; private set; }

    /// <summary>Type de chambre auquel le forfait est reserve. Null = tous les types.</summary>
    public string? RoomTypeCode { get; private set; }

    /// <summary>Premiere date de sejour couverte. Null = pas de borne.</summary>
    public DateOnly? ValidFrom { get; private set; }

    /// <summary>Derniere date de sejour couverte, incluse. Null = pas de borne.</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Nombre de nuits que couvre le prix global. Zero = une nuit.</summary>
    public int Nights { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<PackageComponent> Components => components.AsReadOnly();

    /// <summary>Somme des composantes declarees.</summary>
    public decimal ComponentsTotal => components.Sum(component => component.Amount);

    /// <summary>
    /// Vrai quand la ventilation couvre exactement le prix global. Un forfait non equilibre reste
    /// enregistrable en brouillon mais le service refuse de le vendre : mieux vaut un forfait
    /// inutilisable qu'un chiffre d'affaires reparti au hasard.
    /// </summary>
    public bool IsBalanced => components.Count > 0 && ComponentsTotal == TotalPrice;

    /// <summary>
    /// Remplace la ventilation. Une liste vide efface la ventilation et ramene le forfait a l'etat
    /// non equilibre, ce qui reste un etat de travail legitime.
    /// </summary>
    public void ReplaceComponents(IEnumerable<PackageComponent> newComponents)
    {
        ArgumentNullException.ThrowIfNull(newComponents);

        var materialized = newComponents.ToList();

        if (materialized.Count > 0)
        {
            var total = materialized.Sum(component => component.Amount);

            if (total != TotalPrice)
            {
                throw new ArgumentException(
                    $"La ventilation du forfait totalise {total:0.00} alors que son prix global est "
                    + $"{TotalPrice:0.00}. Corrigez l'un ou l'autre : sans egalite, le chiffre d'affaires "
                    + "des services serait faux.",
                    nameof(newComponents));
            }
        }

        components.Clear();
        components.AddRange(materialized);
    }

    public void UpdateDetails(string label, decimal totalPrice, string? description, int nights)
    {
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
        TotalPrice = LodgingText.Money(totalPrice, nameof(totalPrice));
        Nights = LodgingText.Count(nights, nameof(nights), 365);

        // La ventilation devient caduque des que le prix global change : la laisser en place
        // afficherait un forfait "equilibre" qui ne l'est plus.
        if (components.Count > 0 && ComponentsTotal != TotalPrice)
        {
            components.Clear();
        }
    }

    public void SetScope(string? ratePlanCode, string? roomTypeCode, DateOnly? validFrom, DateOnly? validTo)
    {
        if (validFrom is { } from && validTo is { } to && to < from)
        {
            throw new ArgumentException(
                "La date de fin de validite ne peut pas preceder la date de debut.",
                nameof(validTo));
        }

        RatePlanCode = LodgingText.OptionalCode(ratePlanCode, nameof(ratePlanCode));
        RoomTypeCode = LodgingText.OptionalCode(roomTypeCode, nameof(roomTypeCode));
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    /// <summary>Le forfait est-il vendable pour un sejour commencant a cette date ?</summary>
    public bool IsSellableOn(DateOnly arrival)
    {
        if (!IsActive)
        {
            return false;
        }

        if (ValidFrom is { } from && arrival < from)
        {
            return false;
        }

        return ValidTo is not { } to || arrival <= to;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
