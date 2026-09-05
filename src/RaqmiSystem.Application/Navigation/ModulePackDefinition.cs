using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// La fiche d'un paquet fonctionnel : ce qu'il regroupe et s'il peut être désactivé.
/// </summary>
/// <param name="Pack">La valeur stable, celle qui finira dans les fichiers de licence.</param>
/// <param name="Label">Libellé affiché dans le paramétrage et les écrans de licence.</param>
/// <param name="Description">Ce que le paquet apporte, en une phrase de vente honnête.</param>
/// <param name="AlwaysActive">
/// Vrai pour le socle, le pilotage et le système : une installation sans administration, sans
/// tableau de bord ou sans sauvegarde n'est pas une installation. Ces paquets ne se négocient
/// pas et n'apparaissent dans aucune liste de choix.
/// </param>
public sealed record ModulePackDefinition(
    ModulePack Pack,
    string Label,
    string Description,
    bool AlwaysActive);
