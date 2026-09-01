using System.Windows;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop;

public partial class MainWindow
{
    // Ces deux ecrans ont ete ajoutes apres la liste historique d'ApplyModuleAccess.
    // CanOpenModule bloque deja toute navigation non autorisee, mais les TabItem eux-
    // memes restaient actifs : le cycle clavier pouvait donc les selectionner un
    // instant avant le repli vers l'accueil. Ce garde remet la defense en profondeur
    // au meme niveau que les autres modules Disponibles.
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        MainContentGrid.IsVisibleChanged += MainContentGrid_OnVisibilityChangedForModuleAccess;
        ApplyLateModuleAccess();
    }

    private void MainContentGrid_OnVisibilityChangedForModuleAccess(object sender, DependencyPropertyChangedEventArgs e)
    {
        ApplyLateModuleAccess();
    }

    private void ApplyLateModuleAccess()
    {
        ApplyModuleAccess(PermissionCatalog.LodgingRead, PmsTabItem);
        ApplyModuleAccess(PermissionCatalog.DashboardRead, KpiTabItem);
    }
}
