using RaqmiSystem.Domain.Common;
using RaqmiSystem.Domain.Organization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaqmiSystem.Domain.Lodging;

/// <summary>
/// Une politique d'annulation : combien l'hotel retient selon le moment de l'annulation, et
/// combien il retient en cas de no-show.
///
/// UNE POLITIQUE EST UN BAREME PAR PALIERS, pas une regle unique. "Gratuit jusqu'a J-2, puis une
/// nuit, no-show 100 %" fait trois decisions distinctes, et c'est la forme que prennent toutes les
/// politiques hotelieres reelles. Chaque palier declare le DELAI MINIMAL avant l'arrivee a partir
/// duquel il s'applique ; le palier retenu est le plus genereux qui reste applicable, ce qui rend
/// le bareme lisible dans le sens ou le client le lit.
///
/// LE POINT CRITIQUE : LA POLITIQUE EST FIGEE DANS LE DOSSIER. Un dossier confirme porte le
/// <see cref="ToSnapshotJson"/> de la politique du jour de sa confirmation. Modifier la politique
/// ensuite ne change RIEN aux dossiers deja pris - le client a accepte les conditions affichees ce
/// jour-la, et un bareme qui change retroactivement est indefendable, commercialement comme
/// juridiquement.
/// </summary>
public sealed class CancellationPolicy : AuditableEntity
{
    public const int CodeMaxLength = 40;
    public const int LabelMaxLength = 160;
    public const int DescriptionMaxLength = 1000;

    private readonly List<CancellationPolicyRule> rules = [];

    private CancellationPolicy()
    {
    }

    public CancellationPolicy(string hotelUnitCode, string code, string label, string? description = null)
    {
        HotelUnitCode = HotelUnit.NormalizeCode(hotelUnitCode);
        Code = LodgingText.RequireCode(code, nameof(code), CodeMaxLength);
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
        IsActive = true;
    }

    public string HotelUnitCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Base retenue en cas de no-show. Independante des paliers d'annulation.</summary>
    public CancellationChargeBasis NoShowBasis { get; private set; } = CancellationChargeBasis.FirstNight;

    /// <summary>Valeur associee a <see cref="NoShowBasis"/> (nombre de nuits, %, ou montant).</summary>
    public decimal NoShowValue { get; private set; }

    public IReadOnlyCollection<CancellationPolicyRule> Rules => rules.AsReadOnly();

    /// <summary>
    /// Remplace les paliers. Deux paliers ne peuvent pas partager le meme delai : le bareme
    /// deviendrait ambigu et deux operateurs liraient deux penalites differentes.
    /// </summary>
    public void ReplaceRules(IEnumerable<CancellationPolicyRule> newRules)
    {
        ArgumentNullException.ThrowIfNull(newRules);

        var materialized = newRules.OrderByDescending(rule => rule.MinDaysBeforeArrival).ToList();

        var duplicates = materialized
            .GroupBy(rule => rule.MinDaysBeforeArrival)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicates is not null)
        {
            throw new ArgumentException(
                $"Deux paliers portent le meme delai ({duplicates.Key} jour(s)) : le bareme serait ambigu.",
                nameof(newRules));
        }

        rules.Clear();
        rules.AddRange(materialized);
    }

    public void SetNoShowTerms(CancellationChargeBasis basis, decimal value)
    {
        CancellationPolicyRule.ValidateTerms(basis, value, nameof(value));
        NoShowBasis = basis;
        NoShowValue = value;
    }

    public void UpdateDetails(string label, string? description)
    {
        Label = LodgingText.Require(label, nameof(label), LabelMaxLength);
        Description = LodgingText.Optional(description, nameof(description), DescriptionMaxLength);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Fige la politique dans sa forme de stockage, telle qu'elle sera relue par
    /// <see cref="EvaluateSnapshot"/> des annees plus tard.
    /// </summary>
    public string ToSnapshotJson()
    {
        var document = new PolicySnapshot(
            Code,
            Label,
            NoShowBasis,
            NoShowValue,
            rules.OrderByDescending(rule => rule.MinDaysBeforeArrival)
                .Select(rule => new RuleSnapshot(rule.MinDaysBeforeArrival, rule.Basis, rule.Value))
                .ToArray());

        return JsonSerializer.Serialize(document);
    }

    /// <summary>
    /// Calcule la penalite d'ANNULATION depuis une politique figee.
    ///
    /// <paramref name="daysBeforeArrival"/> peut etre negatif quand l'annulation intervient apres
    /// la date d'arrivee : aucun palier ne s'applique alors, sauf un palier a zero jour, ce qui est
    /// la facon d'ecrire "au-dela, tout est du".
    /// </summary>
    public static decimal EvaluateSnapshot(
        string snapshotJson,
        int daysBeforeArrival,
        decimal totalStayAmount,
        IReadOnlyList<ReservationNightRate> nightlyRates)
    {
        var snapshot = Parse(snapshotJson);

        if (snapshot is null)
        {
            return 0m;
        }

        // Le palier retenu est celui dont le delai est le plus eleve tout en restant atteint : le
        // plus genereux encore applicable. Un bareme lu dans l'autre sens facturerait la penalite
        // maximale a qui annule six mois a l'avance.
        var applicable = snapshot.Rules
            .Where(rule => daysBeforeArrival >= rule.MinDaysBeforeArrival)
            .OrderByDescending(rule => rule.MinDaysBeforeArrival)
            .FirstOrDefault();

        if (applicable is null)
        {
            return 0m;
        }

        return Compute(applicable.Basis, applicable.Value, totalStayAmount, nightlyRates);
    }

    /// <summary>Calcule la penalite de NO-SHOW depuis une politique figee.</summary>
    public static decimal EvaluateNoShowSnapshot(
        string snapshotJson,
        decimal totalStayAmount,
        IReadOnlyList<ReservationNightRate> nightlyRates)
    {
        var snapshot = Parse(snapshotJson);

        return snapshot is null
            ? 0m
            : Compute(snapshot.NoShowBasis, snapshot.NoShowValue, totalStayAmount, nightlyRates);
    }

    /// <summary>Libelle lisible d'une politique figee, pour l'afficher au comptoir.</summary>
    public static string DescribeSnapshot(string snapshotJson)
    {
        var snapshot = Parse(snapshotJson);

        if (snapshot is null)
        {
            return string.Empty;
        }

        var parts = snapshot.Rules
            .OrderByDescending(rule => rule.MinDaysBeforeArrival)
            .Select(rule => $"J-{rule.MinDaysBeforeArrival} : {Describe(rule.Basis, rule.Value)}")
            .ToList();

        parts.Add($"No-show : {Describe(snapshot.NoShowBasis, snapshot.NoShowValue)}");

        return string.Join(" ; ", parts);
    }

    private static PolicySnapshot? Parse(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PolicySnapshot>(snapshotJson);
        }
        catch (JsonException)
        {
            // Une politique figee illisible ne doit jamais faire echouer une annulation : le
            // dossier se ferme sans penalite, et l'ecart se traite a la main.
            return null;
        }
    }

    private static decimal Compute(
        CancellationChargeBasis basis,
        decimal value,
        decimal totalStayAmount,
        IReadOnlyList<ReservationNightRate> nightlyRates)
    {
        var amount = basis switch
        {
            CancellationChargeBasis.None => 0m,
            CancellationChargeBasis.FirstNight => nightlyRates.Count > 0 ? nightlyRates[0].Amount : 0m,
            CancellationChargeBasis.Nights => nightlyRates
                .Take(Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero)))
                .Sum(rate => rate.Amount),
            CancellationChargeBasis.PercentOfStay => totalStayAmount * value / 100m,
            CancellationChargeBasis.FixedAmount => value,
            _ => 0m
        };

        // La penalite ne peut jamais depasser ce que le sejour aurait coute : retenir davantage
        // que le prix de la chambre n'est pas une politique, c'est une erreur de saisie.
        var capped = Math.Min(amount, totalStayAmount);

        return Math.Round(Math.Max(0m, capped), 2, MidpointRounding.AwayFromZero);
    }

    private static string Describe(CancellationChargeBasis basis, decimal value)
    {
        return basis switch
        {
            CancellationChargeBasis.None => "gratuit",
            CancellationChargeBasis.FirstNight => "1 nuit",
            CancellationChargeBasis.Nights => $"{value:0.##} nuit(s)",
            CancellationChargeBasis.PercentOfStay => $"{value:0.##} % du sejour",
            CancellationChargeBasis.FixedAmount => $"{value:0.00} (forfait)",
            _ => "-"
        };
    }

    private sealed record PolicySnapshot(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("noShowBasis")] CancellationChargeBasis NoShowBasis,
        [property: JsonPropertyName("noShowValue")] decimal NoShowValue,
        [property: JsonPropertyName("rules")] RuleSnapshot[] Rules);

    private sealed record RuleSnapshot(
        [property: JsonPropertyName("minDaysBeforeArrival")] int MinDaysBeforeArrival,
        [property: JsonPropertyName("basis")] CancellationChargeBasis Basis,
        [property: JsonPropertyName("value")] decimal Value);
}
