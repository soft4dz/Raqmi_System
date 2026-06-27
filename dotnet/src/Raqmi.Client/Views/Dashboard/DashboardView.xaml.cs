using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Raqmi.Client.Localization;
using Raqmi.Client.Navigation;

namespace Raqmi.Client.Views.Dashboard;

public partial class DashboardView : UserControl
{
    public event Action<string>? ModuleOpenRequested;

    private AppLocale _locale = AppLocale.Fr;
    private string _userName = "Admin";

    private List<ModuleCardVm> _modules = [];

    public DashboardView()
    {
        InitializeComponent();
    }

    public void Bind(DashboardData data, AppLocale locale, string userName)
    {
        _locale = locale;
        _userName = userName;

        var enabled = data.Modules.Count(m => m.Enabled);
        var families = data.Modules.Select(m => m.Family).Distinct().Count();
        var packLabel = ExtractPackLabel(data.License);
        var isValid = data.License.Evaluation.Valid;

        DashboardEyebrow.Text = T("dashboard");
        WelcomeText.Text = AppStrings.Format(_locale, "welcome", ("name", _userName));
        LicenseText.Text = AppStrings.Format(_locale, "packSummary",
            ("pack", packLabel), ("enabled", enabled.ToString()), ("total", data.Modules.Count.ToString()));
        LicenseLabel.Text = T("license");
        LicenseStatusText.Text = isValid ? T("licenseValid") : T("licenseInvalid");
        LicenseStatusText.Foreground = new SolidColorBrush(isValid
            ? (Color)ColorConverter.ConvertFromString("#22C55E")!
            : (Color)ColorConverter.ConvertFromString("#EF4444")!);
        LicenseExpiryText.Text = T("licenseDemo");
        StatModulesLabel.Text = T("activeModules");
        StatFamiliesLabel.Text = T("families");
        StatPackLabel.Text = T("pack");
        StatModeLabel.Text = T("mode");
        StatModules.Text = enabled.ToString();
        StatFamilies.Text = families.ToString();
        StatPack.Text = packLabel;
        StatMode.Text = data.License.Evaluation.ReadonlyMode ? T("readonly") : T("normal");
        ModulesTitle.Text = T("availableModules");
        ModulesHint.Text = T("modulesHint");

        _modules = data.Modules
            .Where(m => ModuleNavigator.DefaultScreenForModule(m.Code) is not null)
            .Select(m => new ModuleCardVm
            {
                Code = m.Code,
                Label = m.Label,
                Description = m.Description,
                IsEnabled = m.Enabled,
                Status = m.Enabled ? T("active") : T("blocked"),
            }).ToList();
        ModulesList.ItemsSource = _modules;
    }

    private void OnModulesListClick(object sender, MouseButtonEventArgs e)
    {
        var border = FindModuleCardBorder(e.OriginalSource as DependencyObject);
        if (border?.Tag is not string code) return;
        var item = _modules.FirstOrDefault(x => x.Code == code);
        if (item is null || !item.IsEnabled) return;
        ModuleOpenRequested?.Invoke(code);
        e.Handled = true;
    }

    private static Border? FindModuleCardBorder(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Border { Tag: string } border) return border;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private string T(string key) => AppStrings.Get(_locale, key);

    private static string ExtractPackLabel(LicenseStatusResponse license)
    {
        if (license.Pack?.Label is { } label) return label;
        try
        {
            var json = JsonSerializer.Serialize(license.License);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("kind", out var kind))
                return kind.GetString() ?? "Professional";
        }
        catch { /* ignore */ }
        return "Professional";
    }

    private sealed class ModuleCardVm
    {
        public required string Code { get; init; }
        public required string Label { get; init; }
        public required string Description { get; init; }
        public required bool IsEnabled { get; init; }
        public required string Status { get; init; }
    }
}
