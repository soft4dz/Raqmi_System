using System.Windows;
using System.Windows.Controls;
using Raqmi.Client.Localization;
using Raqmi.Client.Ui;
using Raqmi.Client.Views.Shell;

namespace Raqmi.Client;

public partial class MainWindow : Window
{
    private readonly RaqmiApiClient _api = new();
    private ClientConfig _config = new();
    private AppLocale _locale = AppLocale.Fr;
    private string _userName = "Administrateur Demo";
    private DashboardData? _lastDashboard;

    public MainWindow()
    {
        InitializeComponent();
        ShellView.LogoutRequested += () =>
        {
            _lastDashboard = null;
            ShowLogin();
        };
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _config = await ConfigStore.LoadAsync();
        _locale = AppLocaleExtensions.FromCode(_config.Locale);
        _config.ServerUrl = ServerUrlNormalizer.Normalize(_config.ServerUrl);
        ServerUrlBox.Text = _config.ServerUrl;
        await _api.ConfigureAsync(_config);
        ApplyLocale();

        if (await _api.TestServerAsync())
            ShowLogin(animate: false);
        else
            ShowSettings(animate: false);
    }

    private async void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        SetError(SettingsErrorPanel, SettingsError, null);
        _config.ServerUrl = ServerUrlNormalizer.Normalize(ServerUrlBox.Text.Trim());
        ServerUrlBox.Text = _config.ServerUrl;
        await _api.ConfigureAsync(_config);

        if (!await _api.TestServerAsync())
        {
            SetError(SettingsErrorPanel, SettingsError, T("serverUnreachable"));
            return;
        }

        await ConfigStore.SaveAsync(_config);
        ShowLogin();
    }

    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        SetError(LoginErrorPanel, LoginError, null);
        try
        {
            await _api.LoginAsync(EmailBox.Text.Trim(), PasswordBox.Password);
            var dashboard = await _api.LoadDashboardAsync();
            _userName = EmailBox.Text.Trim().Split('@')[0];
            ShowDashboard(dashboard);
        }
        catch (Exception ex)
        {
            SetError(LoginErrorPanel, LoginError, ex.Message);
        }
    }

    private async void OnLocaleFr(object sender, RoutedEventArgs e) => await SetLocaleAsync(AppLocale.Fr);
    private async void OnLocaleEn(object sender, RoutedEventArgs e) => await SetLocaleAsync(AppLocale.En);
    private async void OnLocaleAr(object sender, RoutedEventArgs e) => await SetLocaleAsync(AppLocale.Ar);

    private async Task SetLocaleAsync(AppLocale locale)
    {
        if (_locale == locale) return;
        _locale = locale;
        _config.Locale = locale.ToCode();
        await ConfigStore.SaveAsync(_config);
        ApplyLocale();
        if (_lastDashboard is not null)
            ShellView.SetLocale(_locale);
    }

    private void ApplyLocale()
    {
        FlowDirection = _locale.IsRtl() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = T("appName");

        SettingsTitle.Text = T("serverSettings");
        SettingsHint.Text = T("serverSettingsHint");
        ServerUrlLabel.Text = T("serverUrl");
        ServerUrlBox.ToolTip = T("serverUrlExamples");
        SaveSettingsButton.Content = T("saveContinue");

        LoginAppName.Text = T("appName");
        LoginTagline.Text = T("appTagline");
        EmailLabel.Text = T("email");
        PasswordLabel.Text = T("password");
        LoginButton.Content = T("login");
        DemoAccountText.Text = $"{T("demoAccount")} admin@demo.raqmi.local / demo1234";

        LoginAsideTitle.Text = T("loginAsideTitle");
        LoginAsideText.Text = T("loginAsideText");
        LoginAsideItem1.Text = T("loginAsideItem1");
        LoginAsideItem2.Text = T("loginAsideItem2");
        LoginAsideItem3.Text = T("loginAsideItem3");

        UpdateLangPills();
    }

    private void UpdateLangPills()
    {
        LangFr.Style = _locale == AppLocale.Fr ? (Style)FindResource("LangPillActive") : (Style)FindResource("LangPillButton");
        LangEn.Style = _locale == AppLocale.En ? (Style)FindResource("LangPillActive") : (Style)FindResource("LangPillButton");
        LangAr.Style = _locale == AppLocale.Ar ? (Style)FindResource("LangPillActive") : (Style)FindResource("LangPillButton");
    }

    private void ShowSettings(bool animate = true)
    {
        if (animate)
            ViewTransitions.SwitchPanel(LoginPanel, SettingsPanel);
        else
        {
            SettingsPanel.Visibility = Visibility.Visible;
            LoginPanel.Visibility = Visibility.Collapsed;
            ShellView.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowLogin(bool animate = true)
    {
        PasswordBox.Password = "demo1234";
        SetError(LoginErrorPanel, LoginError, null);

        if (animate)
        {
            FrameworkElement from = ShellView.Visibility == Visibility.Visible ? ShellView : SettingsPanel;
            ViewTransitions.SwitchPanel(from, LoginPanel);
        }
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
            ShellView.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowDashboard(DashboardData data)
    {
        _lastDashboard = data;
        ShellView.Initialize(_api, data, _locale, _userName);

        if (LoginPanel.Visibility == Visibility.Visible)
            ViewTransitions.SwitchPanel(LoginPanel, ShellView);
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Collapsed;
            ShellView.Visibility = Visibility.Visible;
            ViewTransitions.FadeIn(ShellView);
        }
    }

    private string T(string key) => AppStrings.Get(_locale, key);

    private static void SetError(Border panel, TextBlock textBlock, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            panel.Visibility = Visibility.Collapsed;
            textBlock.Text = string.Empty;
            return;
        }

        panel.Visibility = Visibility.Visible;
        textBlock.Text = message;
    }
}
