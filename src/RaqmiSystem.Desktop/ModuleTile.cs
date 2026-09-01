using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

// Element d'affichage d'un module, partage par l'ecran d'accueil et la barre
// laterale : les donnees figees du catalogue, l'etat de verrouillage - qui depend
// du profil connecte et change donc a chaque connexion/deconnexion - et le module
// actuellement ouvert. D'ou INotifyPropertyChanged, qui evite de reconstruire la
// collection (et donc de faire clignoter la grille), et qui garantit que les deux
// surfaces disent la meme chose du meme module sans code de synchronisation.
public sealed class ModuleTile : INotifyPropertyChanged
{
    public const string AccessDeniedToolTip = "Accès non autorisé pour votre profil";

    private bool isLocked;
    private bool isActive;
    private readonly FunctionalDomainDefinition functionalDomain;

    public ModuleTile(ModuleCatalogEntry entry)
    {
        Entry = entry;
        functionalDomain = FunctionalArchitectureCatalog.DomainForLegacyOrder(entry.Order);
        StatusLabel = ModuleCatalog.StatusLabel(entry.Status);
        GroupIconKey = functionalDomain.IconKey;
        SearchText = NormalizeForSearch(
            $"{entry.Name} {entry.Description} {functionalDomain.Name} {entry.Group} {entry.Order}");
    }

    /// <summary>
    /// Nom, description, famille et numero d'ordre, normalises une fois pour toutes.
    /// Porte par la tuile plutot que par chacune des deux surfaces qui cherchent
    /// (accueil et barre laterale) : une seule facon de trouver un module, donc jamais
    /// un resultat d'un cote et pas de l'autre.
    /// </summary>
    public string SearchText { get; }

    /// <summary>
    /// Minuscules sans accent : « Hebergement » doit trouver « Hébergement », et « TVA »
    /// comme « tva ». La decomposition Unicode separe la lettre de son accent, qu'il
    /// suffit alors d'ecarter. A appliquer aussi a la saisie avant toute comparaison.
    /// </summary>
    public static string NormalizeForSearch(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public ModuleCatalogEntry Entry { get; }

    public string Order => Entry.Order;

    public string Group => functionalDomain.Name;

    public string FunctionalDomainId => functionalDomain.Id;

    public FunctionalMaturity FunctionalMaturity => functionalDomain.Maturity;

    public string Name => Entry.Name;

    public string Description => Entry.Description;

    public string Priority => Entry.Priority;

    public ModuleStatus Status => Entry.Status;

    public string StatusLabel { get; }

    public string GroupIconKey { get; }

    public string? PermissionKey => Entry.PermissionKey;

    public int? TabIndex => Entry.TabIndex;

    // Verrouille = le profil connecte n'a pas la permission de lecture du module.
    public bool IsLocked
    {
        get => isLocked;
        set
        {
            if (isLocked == value)
            {
                return;
            }

            isLocked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsClickable));
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    // Module affiche dans MainTabs : la barre laterale le met en surbrillance.
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive == value)
            {
                return;
            }

            isActive = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NavTag));
        }
    }

    // Le style ModuleNavButton (Themes/RaqmiTheme.xaml) reagit a Tag="Active" :
    // exposer directement la valeur attendue evite un convertisseur pour un seul
    // booleen.
    public string? NavTag => isActive ? "Active" : null;

    // Une carte n'est cliquable que si un ecran existe (TabIndex renseigne) et
    // que le profil a le droit de le lire.
    public bool IsClickable => Entry.TabIndex.HasValue && !isLocked;

    // Priorite d'information : permission manquante, puis precision de statut
    // (modules partiels), puis description du module.
    public string ToolTipText => isLocked
        ? AccessDeniedToolTip
        : Entry.StatusNote ?? Entry.Description;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
