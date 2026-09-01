using System.Windows;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Desktop;

public partial class MainWindow
{
    // Le PMS front office a ete ajoute apres la liste historique d'ApplyModuleAccess.
    // CanOpenModule bloque deja toute navigation non autorisee, mais l'onglet lui-meme
    // restait active : Ctrl+Tab pouvait donc le selectionner un instant avant le repli
    // vers l'accueil. Ce garde remet la defense en profondeur au meme niveau que les
    // 30 autres modules Disponibles sans dupliquer la logique de permission.
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        MainContentGrid.IsVisibleChanged += MainContentGrid_OnVisibilityChangedForModuleAccess;
        ApplyPmsModuleAccess();
    }

    private void MainContentGrid_OnVisibilityChangedForModuleAccess(object sender, DependencyPropertyChangedEventArgs e)
    {
        ApplyPmsModuleAccess();
    }

    private void ApplyPmsModuleAccess()
    {
        ApplyModuleAccess(PermissionCatalog.LodgingRead, PmsTabItem);
    }
}
