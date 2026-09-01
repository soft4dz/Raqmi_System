using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Le rattachement d'un prefixe de compte du plan comptable a un groupe de gestion
/// (<see cref="KpiAccountGroup"/>). C'est la piece qui permet de construire un GOP, un EBE et
/// des marges a partir des ecritures comptabilisees, sans jamais inventer de nomenclature.
///
/// UN PREFIXE, PAS UN COMPTE. La regle porte sur le debut du code : "60" attrape 60, 601, 6011
/// et ainsi de suite. C'est ce qui permet de decrire un plan comptable entier en quelques
/// lignes plutot qu'en plusieurs centaines, et de continuer a fonctionner quand le comptable
/// cree un nouveau sous-compte. Quand plusieurs prefixes correspondent au meme compte, LE PLUS
/// LONG gagne : declarer "6" en charges non reparties puis "603" en charges departementales
/// est une facon parfaitement legitime d'ecrire une exception.
///
/// AUCUN MAPPING N'EST SEME. Le module ne livre pas de plan comptable, exactement pour la
/// raison exposee par <c>AccountClassCatalog</c> : reproduire de memoire une nomenclature
/// reglementaire presenterait des codes inventes comme une reference legale. Le mapping est une
/// donnee de l'etablissement, saisie et verifiee par son comptable ; tant qu'il est vide, les
/// indicateurs de resultat repondent "donnee manquante" et disent quoi configurer.
/// </summary>
public sealed class KpiAccountMapping : AuditableEntity
{
    public const int MaxPrefixLength = ChartAccount.MaxCodeLength;

    private KpiAccountMapping()
    {
    }

    public KpiAccountMapping(string accountPrefix, KpiAccountGroup group, string label)
    {
        AccountPrefix = NormalizePrefix(accountPrefix);
        Group = RequireGroup(group);
        Label = RequireLabel(label);
        IsActive = true;
    }

    /// <summary>Debut du code de compte auquel la regle s'applique, chiffres uniquement.</summary>
    public string AccountPrefix { get; private set; } = string.Empty;

    public KpiAccountGroup Group { get; private set; }

    /// <summary>Intitule lisible de la regle, affiche dans le detail des indicateurs de resultat.</summary>
    public string Label { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void UpdateDetails(KpiAccountGroup group, string label)
    {
        Group = RequireGroup(group);
        Label = RequireLabel(label);
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
    /// Ce prefixe couvre-t-il ce code de compte ? La comparaison est ordinale : un code de
    /// compte est une suite de chiffres, pas un mot d'une langue.
    /// </summary>
    public bool Covers(string accountCode)
    {
        return !string.IsNullOrWhiteSpace(accountCode)
            && accountCode.StartsWith(AccountPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Le prefixe suit exactement la codification du plan comptable : chiffres uniquement, et
    /// un premier chiffre entre 1 et 7 puisque c'est la classe du compte. Un prefixe qui ne
    /// pourrait correspondre a aucun compte reel est refuse a la saisie plutot que decouvert
    /// plus tard sous la forme d'un GOP silencieusement incomplet.
    /// </summary>
    public static string NormalizePrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Le prefixe de compte est requis.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxPrefixLength)
        {
            throw new ArgumentException(
                $"Le prefixe de compte ne peut pas depasser {MaxPrefixLength} caracteres.",
                nameof(value));
        }

        if (!trimmed.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "Le prefixe de compte ne peut contenir que des chiffres.",
                nameof(value));
        }

        if (trimmed[0] is < '1' or > '7')
        {
            throw new ArgumentException(
                "Le prefixe de compte doit commencer par une classe comptable valide (1 a 7).",
                nameof(value));
        }

        return trimmed;
    }

    private static KpiAccountGroup RequireGroup(KpiAccountGroup group)
    {
        if (!Enum.IsDefined(group))
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Groupe de gestion inconnu.");
        }

        return group;
    }

    private static string RequireLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("L'intitule est requis.", nameof(label));
        }

        var trimmed = label.Trim();

        if (trimmed.Length > 200)
        {
            throw new ArgumentException("L'intitule ne peut pas depasser 200 caracteres.", nameof(label));
        }

        return trimmed;
    }
}
