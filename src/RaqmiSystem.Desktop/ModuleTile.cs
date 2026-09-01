using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private readonly NavigationModulePlacement placement;

    // catalogIndex : rang de l'entree dans ModuleCatalog. Il departage les cartes d'un
    // meme module sur l'accueil, pour que l'ordre editorial du catalogue y survive au tri
    // par module de l'arbre.
    public ModuleTile(ModuleCatalogEntry entry, int catalogIndex = 0)
    {
        Entry = entry;
        CatalogIndex = catalogIndex;
        functionalDomain = FunctionalArchitectureCatalog.DomainForLegacyOrder(entry.Order);
        placement = FunctionalArchitectureCatalog.PlacementForLegacyOrder(entry.Order);
        StatusLabel = ModuleCatalog.StatusLabel(entry.Status);
        Maturity = FunctionalMaturityMapper.FromLegacyStatus(ToLegacyStatus(entry.Status));
        MaturityLabel = FunctionalMaturityMapper.Label(Maturity);
        GroupIconKey = functionalDomain.IconKey;
        HomeGroup = $"{placement.Domain.Id} · {placement.Domain.Label}  →  {placement.Module.Label}";
        HomeGroupRank = placement.ModuleRank;
        SearchText = NormalizeForSearch(
            $"{entry.Name} {entry.Description} {functionalDomain.Name} {placement.Module.Label} {entry.Group} {entry.Order}");
    }

    /// <summary>
    /// Nom, description, domaine, module et numero d'ordre, normalises une fois pour toutes.
    /// Porte par la tuile plutot que par chacune des deux surfaces qui cherchent
    /// (accueil et barre laterale) : une seule facon de trouver un module, donc jamais
    /// un resultat d'un cote et pas de l'autre.
    /// </summary>
    public string SearchText { get; }

    /// <summary>
    /// Minuscules sans accent. La normalisation vit dans Application.Navigation
    /// (<see cref="NavigationSearch.Normalize"/>), ou l'arbre de navigation et ses tests la
    /// partagent ; cette methode reste pour les appelants existants.
    /// </summary>
    public static string NormalizeForSearch(string value) => NavigationSearch.Normalize(value);

    public ModuleCatalogEntry Entry { get; }

    public int CatalogIndex { get; }

    public string Order => Entry.Order;

    public string Group => functionalDomain.Name;

    public string FunctionalDomainId => functionalDomain.Id;

    public FunctionalMaturity FunctionalMaturity => functionalDomain.Maturity;

    /// <summary>
    /// Maturite de CE module (statut du catalogue converti en quatre niveaux), a ne pas
    /// confondre avec celle de son domaine : un ecran fonctionnel peut vivre dans un
    /// domaine encore en apercu technique.
    /// </summary>
    public FunctionalMaturity Maturity { get; }

    public string MaturityLabel { get; }

    /// <summary>
    /// Cle de regroupement de l'accueil : « 06 · PMS / Hébergement  →  Front Office ».
    /// Chaine et non objet, parce que l'en-tete de groupe du theme affiche
    /// <c>CollectionViewGroup.Name</c> tel quel.
    /// </summary>
    public string HomeGroup { get; }

    /// <summary>Rang du module dans l'arbre : l'ordre des groupes de l'accueil.</summary>
    public int HomeGroupRank { get; }

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

    // Passage par le nom, pas par la valeur numerique : les deux enumerations ont les memes
    // membres, mais rien n'oblige a ce qu'elles gardent le meme ordre.
    private static LegacyModuleStatus ToLegacyStatus(ModuleStatus status) => status switch
    {
        ModuleStatus.Disponible => LegacyModuleStatus.Disponible,
        ModuleStatus.ApiPrete => LegacyModuleStatus.ApiPrete,
        ModuleStatus.Partiel => LegacyModuleStatus.Partiel,
        ModuleStatus.Planifie => LegacyModuleStatus.Planifie,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Statut de module inconnu.")
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
