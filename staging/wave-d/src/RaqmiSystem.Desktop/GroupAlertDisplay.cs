using RaqmiSystem.Application.Pilotage;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Single source of the French display labels for the CEO dashboard's direction alerts
/// (<see cref="GroupAlertType"/> and <see cref="GroupAlertSeverity"/>): the alerts grid, its
/// tooltips and any export must all render the same wording instead of the raw enum names.
/// The rule tooltips are the FRENCH READING of the server's own rule sentences (GroupAlert.Rule
/// and GroupDashboardBasis): they mirror the server's rules, they never replace them.
/// </summary>
public static class GroupAlertDisplay
{
    public static string ToFrench(GroupAlertType type)
    {
        return type switch
        {
            GroupAlertType.UnclosedDays => "Jours non clôturés",
            GroupAlertType.PendingValidation => "Recettes en attente de validation",
            GroupAlertType.OverdueInvoices => "Factures impayées de plus de 60 jours",
            _ => type.ToString()
        };
    }

    public static string ToFrench(GroupAlertSeverity severity)
    {
        return severity switch
        {
            GroupAlertSeverity.Info => "Info",
            GroupAlertSeverity.Attention => "Attention",
            _ => severity.ToString()
        };
    }

    public static string RuleToFrench(GroupAlertType type)
    {
        return type switch
        {
            GroupAlertType.UnclosedDays =>
                "Règle : journées passées de la période (strictement avant aujourd'hui) sans clôture " +
                "au statut « Clôturée » pour cette unité — une journée rouverte compte comme non " +
                "clôturée tant qu'elle n'est pas re-clôturée.",
            GroupAlertType.PendingValidation =>
                "Règle : recettes journalières au statut « Soumise » depuis plus de 48 heures — une " +
                "recette soumise n'est pas comptée comme réalisée tant qu'elle n'est pas validée.",
            GroupAlertType.OverdueInvoices =>
                "Règle : factures émises non payées dont l'ancienneté à la fin de période dépasse " +
                "60 jours (tranches 61–90 et +90 de la balance âgée) ; l'ancienneté part de la date " +
                "de facture, le système ne gérant pas d'échéance.",
            _ => string.Empty
        };
    }
}
