using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RaqmiSystem.Desktop;

namespace RaqmiSystem.DocShots;

// Campagne de captures du guide utilisateur.
//
// Le principe : ouvrir la vraie MainWindow du client, s'y connecter avec un compte de
// demonstration, puis parcourir les onglets des modules livres et rendre chacun en PNG.
// Rien n'est simule - ce que le guide montre est ce que l'utilisateur verra.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        ShotOptions options;
        try
        {
            options = ShotOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(ShotOptions.Usage);
            return 2;
        }

        var exitCode = 0;

        var app = new App
        {
            // Sans cela l'application ouvrirait MainWindow toute seule (StartupUri de
            // App.xaml) : la campagne piloterait une fenetre, l'utilisateur en verrait deux.
            StartupUri = null,

            // La campagne decide seule de sa fin : une fenetre fermee en cours de route ne
            // doit pas couper le processus avant l'ecriture du manifeste.
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        app.InitializeComponent();

        app.Startup += async (_, _) =>
        {
            try
            {
                await RunCampaignAsync(options);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("ECHEC : " + exception.Message);
                Console.Error.WriteLine(exception);
                exitCode = 1;
            }
            finally
            {
                app.Shutdown();
            }
        };

        app.Run();
        return exitCode;
    }

    private static async Task RunCampaignAsync(ShotOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        var window = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 40,
            Top = 20,
            Width = options.Width,
            Height = options.Height
        };

        window.Show();
        await WaitForIdleAsync(window);

        await SignInAsync(window, options);

        var targets = CaptureTarget.BuildAll();
        Console.WriteLine(targets.Count + " ecrans a capturer vers " + options.OutputDirectory);

        var captured = new List<CaptureTarget>(targets.Count);

        foreach (var target in targets)
        {
            var tabs = Field<TabControl>(window, "MainTabs");
            tabs.SelectedIndex = target.TabIndex;

            // Le chargement d'un module est asynchrone (EnsureModuleTabLoadedAsync) et
            // aucun evenement public n'en signale la fin : on laisse le temps convenu,
            // puis on attend que le dispatcher n'ait plus rien a faire.
            await Task.Delay(options.DelayMilliseconds);
            await WaitForIdleAsync(window);

            var path = Path.Combine(options.OutputDirectory, target.FileName);
            Capture(window, path, options.Scale);

            captured.Add(target);
            Console.WriteLine("  [" + captured.Count + "/" + targets.Count + "] " + target.FileName + "  (" + target.Title + ")");
        }

        WriteManifest(options, captured);
        Console.WriteLine("Termine : " + captured.Count + " captures + manifest.json");

        window.Close();
    }

    // Connexion par le vrai bouton de l'ecran de connexion : le meme code que celui d'un
    // utilisateur, donc les memes appels API, le meme journal d'audit et les memes
    // permissions appliquees aux ecrans.
    private static async Task SignInAsync(MainWindow window, ShotOptions options)
    {
        Field<TextBox>(window, "ApiBaseUrlTextBox").Text = options.ApiBaseUrl;
        Field<TextBox>(window, "UserNameTextBox").Text = options.UserName;
        Field<PasswordBox>(window, "PasswordBox").Password = options.Password;

        // Decoche volontairement : la campagne ne doit rien ecrire dans les identifiants
        // memorises du poste. Le script appelant sauvegarde et restaure le fichier de
        // reglages, ceci evite simplement d'y ecrire un compte de demonstration.
        Field<CheckBox>(window, "RememberMeCheckBox").IsChecked = false;

        Field<Button>(window, "LoginButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        var content = Field<Grid>(window, "MainContentGrid");
        var connected = await WaitUntilAsync(
            () => content.Visibility == Visibility.Visible,
            TimeSpan.FromSeconds(45));

        if (!connected)
        {
            throw new InvalidOperationException(
                "Connexion impossible a " + options.ApiBaseUrl + " avec " + options.UserName + ".");
        }

        Console.WriteLine("Connecte a " + options.ApiBaseUrl + " en tant que " + options.UserName);
        await Task.Delay(1500);
        await WaitForIdleAsync(window);
    }

    // Rendu de l'arbre visuel, pas du bureau : independant de la resolution du poste,
    // sans fenetre parasite, et net a l'echelle demandee.
    private static void Capture(Window window, string path, double scale)
    {
        var root = (FrameworkElement)window.Content;
        root.UpdateLayout();

        var width = root.ActualWidth;
        var height = root.ActualHeight;

        // Le fond de la fenetre est porte par Window.Background, hors du contenu : rendu
        // seul, le contenu laisserait transparente la marge de 24 px qui l'entoure.
        var composed = new DrawingVisual();
        using (var context = composed.RenderOpen())
        {
            var area = new Rect(0, 0, width, height);
            context.DrawRectangle(window.Background ?? Brushes.White, null, area);
            context.DrawRectangle(new VisualBrush(root), null, area);
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Round(width * scale),
            (int)Math.Round(height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);

        bitmap.Render(composed);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void WriteManifest(ShotOptions options, IReadOnlyList<CaptureTarget> targets)
    {
        var manifest = new CaptureManifest(
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            options.ApiBaseUrl,
            options.Width,
            options.Height,
            options.Scale,
            targets);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(Path.Combine(options.OutputDirectory, "manifest.json"), json, new UTF8Encoding(false));
    }

    // Les champs nommes en XAML sont internes a l'assembly du client : la reflexion evite
    // d'elargir leur visibilite pour un besoin de documentation.
    private static T Field<T>(object instance, string name)
        where T : class
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("Champ introuvable dans MainWindow : " + name + ".");

        return field.GetValue(instance) as T
            ?? throw new InvalidOperationException("Le champ " + name + " n'est pas un " + typeof(T).Name + " renseigne.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(150);
        }

        return condition();
    }

    private static async Task WaitForIdleAsync(DispatcherObject target)
    {
        await target.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await target.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }
}

internal sealed record CaptureManifest(
    string CapturedAtUtc,
    string ApiBaseUrl,
    double WindowWidth,
    double WindowHeight,
    double Scale,
    IReadOnlyList<CaptureTarget> Screens);
