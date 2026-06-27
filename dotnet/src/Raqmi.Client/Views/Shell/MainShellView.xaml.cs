using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Raqmi.Client.Localization;
using Raqmi.Client.Navigation;
using Raqmi.Client.Ui;
using Raqmi.Client.Views.Dashboard;

namespace Raqmi.Client.Views.Shell;

public partial class MainShellView : UserControl
{
    private RaqmiApiClient? _api;
    private ModuleNavigator? _navigator;
    private DashboardView? _dashboard;
    private DashboardData? _dashboardData;
    private AppLocale _locale = AppLocale.Fr;
    private string _userName = "Admin";
    private string _currentScreen = "dashboard";

    public event Action? LogoutRequested;

    public MainShellView()
    {
        InitializeComponent();
    }

    public void Initialize(RaqmiApiClient api, DashboardData data, AppLocale locale, string userName)
    {
        _api = api;
        _navigator = new ModuleNavigator(api);
        _dashboardData = data;
        _locale = locale;
        _userName = userName;

        UserChip.Text = userName;
        LogoutButton.Content = AppStrings.Get(locale, "logout");
        TenantText.Text = ExtractTenantName(data.License);

        BuildSidebar(data.Modules);
        ShowDashboard();
    }

    public void SetLocale(AppLocale locale)
    {
        _locale = locale;
        LogoutButton.Content = AppStrings.Get(locale, "logout");
        if (_dashboardData is not null)
        {
            BuildSidebar(_dashboardData.Modules);
            if (_currentScreen == "dashboard")
                ShowDashboard();
            else
                NavigateTo(_currentScreen);
        }
    }

    public void NavigateToModule(string moduleCode)
    {
        var screen = ModuleNavigator.DefaultScreenForModule(moduleCode);
        if (screen is not null) NavigateTo(screen);
    }

    private void BuildSidebar(IEnumerable<ModuleDto> modules)
    {
        NavPanel.Children.Clear();
        var groups = ModuleNavigator.BuildNavItems(modules)
            .GroupBy(x => x.Family)
            .OrderBy(g => FamilyOrder(g.Key));

        foreach (var group in groups)
        {
            NavPanel.Children.Add(new TextBlock
            {
                Text = AppStrings.Family(_locale, group.Key),
                Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 12, 4, 6),
            });

            foreach (var item in group)
            {
                var button = new Button
                {
                    Content = item.Label,
                    Tag = item.Key,
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Style = (Style)FindResource("GhostButton"),
                };
                button.Click += (_, _) => NavigateTo(item.Key);
                NavPanel.Children.Add(button);
            }
        }
    }

    private static int FamilyOrder(string family) => family switch
    {
        "core" => 0,
        "finance" => 1,
        "hr" => 2,
        "operations" => 3,
        "system" => 4,
        _ => 99,
    };

    private void ShowDashboard()
    {
        _currentScreen = "dashboard";
        ScreenTitle.Text = AppStrings.Get(_locale, "dashboard");
        _dashboard ??= new DashboardView();
        _dashboard.ModuleOpenRequested -= OnModuleOpenRequested;
        _dashboard.ModuleOpenRequested += OnModuleOpenRequested;
        if (_dashboardData is not null)
            _dashboard.Bind(_dashboardData, _locale, _userName);
        ContentHost.Content = _dashboard;
        ViewTransitions.FadeIn(_dashboard);
    }

    private void NavigateTo(string screenKey)
    {
        if (screenKey == "dashboard")
        {
            ShowDashboard();
            return;
        }

        if (_navigator is null) return;

        _currentScreen = screenKey;
        var navItem = ModuleNavigator.BuildNavItems(_dashboardData?.Modules ?? []).FirstOrDefault(x => x.Key == screenKey);
        ScreenTitle.Text = navItem?.Label ?? screenKey;

        try
        {
            var view = _navigator.CreateView(screenKey);
            ContentHost.Content = view;
            ViewTransitions.FadeIn(view);
        }
        catch
        {
            ShowDashboard();
        }
    }

    private void OnModuleOpenRequested(string moduleCode) => NavigateToModule(moduleCode);

    private void OnLogout(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke();

    private static string ExtractTenantName(LicenseStatusResponse license)
    {
        try
        {
            var json = JsonSerializer.Serialize(license.Tenant);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("name", out var name))
                return name.GetString() ?? "Client";
        }
        catch { /* ignore */ }
        return "Hotel Demo Raqmi";
    }
}
