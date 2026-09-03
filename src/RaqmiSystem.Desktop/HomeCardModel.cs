using System.ComponentModel;
using System.Runtime.CompilerServices;
using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Ce qu'une carte de file de travail affiche, dans la forme que le gabarit WPF attend.
/// </summary>
/// <remarks>
/// La carte est creee des la composition, en etat « Chargement », puis mise a jour quand
/// SA source repond : les cartes se remplissent au fil des reponses plutot que toutes a la
/// fin, et une source en echec ne fait basculer que ses cartes. D'ou
/// <see cref="INotifyPropertyChanged"/> et non une collection reconstruite - la carte ne
/// clignote pas, et le focus clavier ne saute pas d'un bouton a l'autre.
///
/// Aucun calcul ici : les chiffres viennent de <see cref="HomeProjection"/>, qui lit les
/// champs deja agreges par le serveur.
/// </remarks>
public sealed class HomeCardModel : INotifyPropertyChanged
{
    private const string LoadingButtonText = "Chargement…";

    private HomeCard card;

    public HomeCardModel(HomeSlot slot, string scopeLabel, string targetLabel)
    {
        ArgumentNullException.ThrowIfNull(slot);

        Slot = slot;
        ScopeLabel = scopeLabel;
        TargetLabel = targetLabel;
        card = new HomeCard(slot, slot.Queue.Label, slot.Queue.Band, HomeCardState.Loading, string.Empty, null, string.Empty, false, false);
    }

    public HomeSlot Slot { get; }

    /// <summary>Code d'unite, « Groupe · toutes unités », « Ma décision » ou « Système ».</summary>
    public string ScopeLabel { get; }

    /// <summary>Ecran que la carte ouvre : dit d'ou vient le chiffre et ou l'on va.</summary>
    public string TargetLabel { get; }

    public string IconKey { get; init; } = string.Empty;

    public int TargetTab => Slot.TargetTab;

    /// <summary>Ni la cible ni le repli ne sont ouvrables : bouton verrouille, chiffre lisible.</summary>
    public bool IsTargetLocked => Slot.TargetLocked;

    public HomeCard Card
    {
        get => card;
        set
        {
            card = value;

            // Tout change d'un coup quand la source repond : une notification par
            // propriete serait plus bavarde sans etre plus juste.
            OnPropertyChanged(nameof(Card));
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(Band));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(AmountText));
            OnPropertyChanged(nameof(HasAmount));
            OnPropertyChanged(nameof(Legend));
            OnPropertyChanged(nameof(IsZero));
            OnPropertyChanged(nameof(IsHidden));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsUnavailable));
            OnPropertyChanged(nameof(IsMuted));
            OnPropertyChanged(nameof(ButtonText));
            OnPropertyChanged(nameof(IsButtonEnabled));
            OnPropertyChanged(nameof(ButtonToolTip));
            OnPropertyChanged(nameof(AutomationName));
        }
    }

    public string Label => card.Label;

    public HomeBand Band => card.Band;

    public HomeCardState State => card.State;

    public string CountText => card.CountText;

    public string? AmountText => card.AmountText;

    public bool HasAmount => !string.IsNullOrEmpty(card.AmountText);

    public string Legend => card.Legend;

    public bool IsZero => card.IsZero;

    public bool IsHidden => card.IsHidden;

    public bool IsLoading => card.State == HomeCardState.Loading;

    public bool IsUnavailable => card.State == HomeCardState.Unavailable;

    /// <summary>
    /// Mode Suivi, compteur a zero ou source muette : la carte s'efface sans disparaitre.
    /// Pas pendant le chargement : le squelette doit rester visible sur son fond.
    /// </summary>
    public bool IsMuted => card.State != HomeCardState.Loading
        && (Slot.Mode == HomeMode.Watch || card.IsZero || card.State != HomeCardState.Ready);

    /// <summary>Le profil peut agir : le bouton porte le verbe du registre.</summary>
    public bool IsAct => Slot.Mode == HomeMode.Act;

    /// <summary>Le profil lit une file qu'il ne peut pas traiter : pastille « Suivi ».</summary>
    public bool IsWatch => Slot.Mode == HomeMode.Watch;

    public string ButtonText => card.State switch
    {
        HomeCardState.Loading => LoadingButtonText,
        _ => Slot.Mode == HomeMode.Act ? Slot.Queue.ActVerb : Slot.Queue.WatchVerb
    };

    // Un bouton desactive pendant le chargement ou sur une cible verrouillee : dans les
    // deux cas le clic n'aurait rien a ouvrir, et le dire vaut mieux que le laisser croire.
    public bool IsButtonEnabled => !IsTargetLocked && card.State != HomeCardState.Loading;

    public string ButtonToolTip => IsTargetLocked
        ? ModuleTile.AccessDeniedToolTip
        : $"Ouvrir {TargetLabel}";

    /// <summary>
    /// La phrase que le lecteur d'ecran annonce. La couleur n'est jamais seule : ce que la
    /// pastille dit en teinte, ce nom le dit en mots.
    /// </summary>
    public string AutomationName
    {
        get
        {
            if (card.State == HomeCardState.Loading)
            {
                return $"{card.Label}, chargement";
            }

            if (card.State == HomeCardState.Unavailable)
            {
                return $"{card.Label}, indisponible";
            }

            var parts = new List<string> { card.Label, card.CountText };

            if (card.AmountText is { } amount)
            {
                parts.Add(amount);
            }

            parts.Add(ScopeLabel);

            parts.Add(IsTargetLocked
                ? "écran non autorisé pour votre profil"
                : Slot.Mode switch
                {
                    HomeMode.Act => "à faire",
                    HomeMode.Watch => "suivi",
                    _ => "information"
                });

            return string.Join(", ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
