using System.Windows;
using System.Windows.Input;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Saisie modale d'UNE valeur, utilisee par les gestes du comptoir qui n'ont besoin que d'une
/// information : un motif, un numero de chambre, une date de remise en service.
/// </summary>
public partial class PmsPromptWindow : Window
{
    public PmsPromptWindow(string message, string caption, string initialValue)
    {
        InitializeComponent();

        Title = caption;
        MessageText.Text = message;
        AnswerTextBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            AnswerTextBox.Focus();
            AnswerTextBox.SelectAll();
        };
    }

    /// <summary>La valeur saisie. Vide est une reponse VALIDE, distincte d'une annulation.</summary>
    public string Answer => AnswerTextBox.Text;

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void AnswerTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
            e.Handled = true;
        }
    }
}
