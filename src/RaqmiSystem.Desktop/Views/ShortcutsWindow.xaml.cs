using System.Windows;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Liste des raccourcis clavier, ouverte par F1 depuis n'importe quel ecran.
/// Fenetre d'information seule : elle n'a ni etat ni action, et se ferme par Echap,
/// par Entree ou par son bouton.
/// </summary>
public partial class ShortcutsWindow : Window
{
    public ShortcutsWindow()
    {
        InitializeComponent();
    }
}
