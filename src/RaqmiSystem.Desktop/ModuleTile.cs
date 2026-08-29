using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RaqmiSystem.Desktop;

// Element d'affichage d'un module sur l'ecran d'accueil : les donnees figees du
// catalogue plus l'etat de verrouillage, qui depend du profil connecte et change
// donc a chaque connexion/deconnexion - d'ou INotifyPropertyChanged, qui evite
// de reconstruire la collection (et donc de faire clignoter la grille).
public sealed class ModuleTile : INotifyPropertyChanged
{
    public const string AccessDeniedToolTip = "Accès non autorisé pour votre profil";

    private bool isLocked;

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
