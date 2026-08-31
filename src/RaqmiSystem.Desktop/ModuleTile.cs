using System.ComponentModel;
using System.Runtime.CompilerServices;

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

    public ModuleTile(ModuleCatalogEntry entry)
    {
        Entry = entry;
        StatusLabel = ModuleCatalog.StatusLabel(entry.Status);
        GroupIconKey = ModuleCatalog.GroupIconKey(entry.Group);
    }

    public ModuleCatalogEntry Entry { get; }

    public string Order => Entry.Order;

    public string Group => Entry.Group;

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
