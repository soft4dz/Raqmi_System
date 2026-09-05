using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RaqmiSystem.Desktop.Api;

namespace RaqmiSystem.Desktop.Views;

/// <summary>
/// Apercu minimal d'un document recu du serveur : metadonnees, enregistrer sous, ouvrir, imprimer.
/// Aucun moteur de rendu embarque : le PDF est ecrit dans un fichier temporaire du poste et confie
/// au shell Windows (verbes « open » et « print » du lecteur PDF par defaut).
///
/// Cablage prevu a l'integration (bouton « Imprimer » d'InvoicesView) :
/// <code>
/// var document = await active.ApiClient.DownloadInvoicePdfAsync(active.ApiBaseUrl, selected.Id);
/// new DocumentPreviewWindow(document) { Owner = Window.GetWindow(this) }.ShowDialog();
/// </code>
/// </summary>
public partial class DocumentPreviewWindow : Window
{
    private readonly DownloadedDocument document;

    private string? temporaryPath;

    public DocumentPreviewWindow(DownloadedDocument document)
    {
        this.document = document ?? throw new ArgumentNullException(nameof(document));

        InitializeComponent();

        Title = $"Document - {document.FileName}";
        TitleText.Text = document.FileName;
        FileNameText.Text = document.FileName;
        ContentTypeText.Text = document.ContentType;
        SizeText.Text = FormatSize(document.Content.LongLength);
        Sha256Text.Text = document.LocalSha256;

        // Une piece dont l'empreinte diverge de celle annoncee par le serveur ne se remet pas :
        // les gestes qui la feraient sortir du poste (ouvrir, imprimer) sont fermes. Enregistrer
        // sous reste possible pour le diagnostic.
        switch (document.IntegrityVerified)
        {
            case true:
                IntegrityText.Text = "Intégrité vérifiée : l'empreinte SHA-256 du fichier reçu est celle archivée par le serveur.";
                break;

            case false:
                IntegrityText.Text = "ATTENTION : l'empreinte du fichier reçu diffère de celle annoncée par le serveur. "
                    + "Ne remettez pas ce document ; recommencez le téléchargement.";
                IntegrityText.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                OpenButton.IsEnabled = false;
                PrintButton.IsEnabled = false;
                break;

            default:
                IntegrityText.Text = "Le serveur n'a pas transmis d'empreinte : intégrité non vérifiée.";
                break;
        }
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = SanitizeFileName(document.FileName),
            DefaultExt = ".pdf",
            AddExtension = true,
            Filter = "Document PDF (*.pdf)|*.pdf",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, document.Content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Enregistrer sous", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchWithShell("open");
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (!LaunchWithShell("print"))
        {
            // Aucune application du poste n'expose le verbe « print » pour les PDF : on ouvre le
            // document, l'operateur imprime depuis son lecteur. C'est le comportement voulu
            // plutot qu'un moteur d'impression maison qui reproduirait moins bien la piece.
            MessageBox.Show(
                this,
                "Aucune application de ce poste n'accepte l'impression directe des PDF. "
                + "Le document va s'ouvrir dans le lecteur par défaut : utilisez sa commande Imprimer.",
                "Impression",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LaunchWithShell("open");
        }
    }

    /// <summary>
    /// Confie le fichier temporaire au shell avec le verbe demande. Faux si le shell n'a pas
    /// d'association pour ce verbe (Win32Exception) ; les autres erreurs sont affichees.
    /// </summary>
    private bool LaunchWithShell(string verb)
    {
        try
        {
            var path = EnsureTemporaryFile();

            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = verb
            });

            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Document", MessageBoxButton.OK, MessageBoxImage.Error);
            return true;
        }
    }

    /// <summary>
    /// Le shell travaille sur des fichiers : le PDF est ecrit une fois dans le dossier temporaire
    /// du poste, sous son nom legal, et reutilise par les gestes suivants de la meme fenetre.
    /// </summary>
    private string EnsureTemporaryFile()
    {
        if (temporaryPath is not null && File.Exists(temporaryPath))
        {
            return temporaryPath;
        }

        var directory = Path.Combine(Path.GetTempPath(), "RaqmiSystem", "Documents");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, SanitizeFileName(document.FileName));
        File.WriteAllBytes(path, document.Content);

        temporaryPath = path;
        return path;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "document.pdf" : sanitized;
    }

    private static string FormatSize(long bytes)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        return bytes switch
        {
            < 1024 => string.Format(culture, "{0:N0} octets", bytes),
            < 1024 * 1024 => string.Format(culture, "{0:N1} Ko ({1:N0} octets)", bytes / 1024d, bytes),
            _ => string.Format(culture, "{0:N2} Mo ({1:N0} octets)", bytes / (1024d * 1024d), bytes)
        };
    }
}
