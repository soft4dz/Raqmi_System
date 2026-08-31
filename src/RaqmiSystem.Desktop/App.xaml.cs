using System.Windows;
using System.Windows.Threading;

namespace RaqmiSystem.Desktop;

public partial class App : System.Windows.Application
{
    // Filet de dernier recours. Les gestionnaires d'evenements WPF sont declares async void :
    // une exception qui s'en echappe ne peut etre attrapee par aucun appelant et remonte
    // directement au Dispatcher, qui termine le processus. Sans ce filet, un poste perdait la
    // saisie en cours ET la fenetre, sans le moindre message.
    //
    // Le filet ne remplace pas la gestion d'erreur des ecrans (RunApiActionAsync attrape et
    // affiche les cas connus) : il couvre ce que personne n'a prevu. On prefere une application
    // vivante affichant un message a une fermeture brutale, car l'utilisateur peut au moins
    // relire ce qu'il avait saisi et le reporter ailleurs avant de relancer.
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Avant la premiere fenetre, et c'est une condition et non un confort : le theme
        // remplace les entrees du dictionnaire de ressources, or une reference
        // {StaticResource} deja resolue garde l'objet qu'elle a capture. Pose ici, aucune
        // couleur n'a encore ete resolue, donc le theme choisi s'applique entierement.
        // Voir l'en-tete de ThemeManager pour le detail du mecanisme.
        ThemeManager.Appliquer(Resources, DesktopSettings.LoadApparence());
        ThemeManager.AppliquerDensite(Resources, DesktopSettings.LoadDensite());

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // e.Handled = true empeche la fermeture. L'etat de l'application peut etre incoherent
        // apres une exception imprevue : on le dit franchement plutot que de laisser croire que
        // tout va bien, et on invite a redemarrer une fois la saisie mise a l'abri.
        e.Handled = true;

        MessageBox.Show(
            "Une erreur imprevue s'est produite.\n\n"
            + e.Exception.Message
            + "\n\nL'application reste ouverte pour vous permettre de noter votre saisie en cours, "
            + "mais son etat peut etre incoherent : redemarrez-la des que possible.",
            "Erreur imprevue",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
