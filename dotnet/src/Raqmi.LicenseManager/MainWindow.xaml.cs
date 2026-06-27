using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Raqmi.Licensing;
using Raqmi.Shared;

namespace Raqmi.LicenseManager;

public partial class MainWindow : Window
{
    private EditorData _data = new();
    private EditorTenant? _selectedTenant;
    private EditorLicense? _selectedLicense;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PackBox.ItemsSource = LicensePacks.All.Select(p => p.Kind).Cast<LicenseKind>().ToList();
        StatusBox.ItemsSource = Enum.GetValues<LicenseStatus>();
        _data = await EditorStore.LoadAsync();

        if (_data.Tenants.Count == 0)
        {
            var tenant = new EditorTenant { Name = "Hotel Demo Raqmi", Code = "demo-hotel" };
            var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
            var license = new EditorLicense
            {
                TenantId = tenant.Id,
                AllowedModules = pack.Modules,
                Limits = pack.DefaultLimits,
            };
            _data.Tenants.Add(tenant);
            _data.Licenses.Add(license);
            await EditorStore.SaveAsync(_data);
        }

        TenantsList.ItemsSource = _data.Tenants;
        TenantsList.SelectedIndex = 0;
    }

    private void OnTenantChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTenant = TenantsList.SelectedItem as EditorTenant;
        _selectedLicense = _data.Licenses.FirstOrDefault(l => l.TenantId == _selectedTenant?.Id);
        if (_selectedTenant is null || _selectedLicense is null) return;

        TitleText.Text = $"Licence — {_selectedTenant.Name}";
        PackBox.SelectedItem = _selectedLicense.Kind;
        StatusBox.SelectedItem = _selectedLicense.Status;
        FingerprintBox.Text = _selectedLicense.ServerFingerprint ?? string.Empty;
        StartDate.SelectedDate = _selectedLicense.StartsAt.Date;
        EndDate.SelectedDate = _selectedLicense.ExpiresAt.Date;
        RenderModules();
    }

    private void RenderModules()
    {
        if (_selectedLicense is null) return;
        ModulesList.Items.Clear();
        foreach (var module in ModuleCatalog.All.Where(m => m.Commercial))
        {
            var wire = RaqmiModuleCodes.ToWire(module.Code);
            var check = new CheckBox
            {
                Content = module.Label,
                IsChecked = _selectedLicense.AllowedModules.Contains(wire),
                Margin = new Thickness(0, 0, 0, 6),
            };
            check.Checked += (_, _) => ToggleModule(wire, true);
            check.Unchecked += (_, _) => ToggleModule(wire, false);
            ModulesList.Items.Add(check);
        }
    }

    private void ToggleModule(string wire, bool enabled)
    {
        if (_selectedLicense is null) return;
        if (enabled && !_selectedLicense.AllowedModules.Contains(wire))
        {
            _selectedLicense.AllowedModules.Add(wire);
        }
        else if (!enabled)
        {
            _selectedLicense.AllowedModules.Remove(wire);
        }

        _ = EditorStore.SaveAsync(_data);
    }

    private async void OnAddTenant(object sender, RoutedEventArgs e)
    {
        var tenant = new EditorTenant();
        var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
        var license = new EditorLicense
        {
            TenantId = tenant.Id,
            AllowedModules = pack.Modules,
            Limits = pack.DefaultLimits,
        };
        _data.Tenants.Add(tenant);
        _data.Licenses.Add(license);
        await EditorStore.SaveAsync(_data);
        TenantsList.ItemsSource = null;
        TenantsList.ItemsSource = _data.Tenants;
        TenantsList.SelectedItem = tenant;
    }

    private void OnPackChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedLicense is null || PackBox.SelectedItem is not LicenseKind kind) return;
        var pack = LicensePacks.All.FirstOrDefault(p => p.Kind == kind);
        if (pack is null) return;
        _selectedLicense.Kind = kind;
        _selectedLicense.AllowedModules = pack.Modules.ToList();
        _selectedLicense.Limits = pack.DefaultLimits;
        RenderModules();
        _ = EditorStore.SaveAsync(_data);
    }

    private async void OnActivate(object sender, RoutedEventArgs e) => await UpdateStatus(LicenseStatus.Active);
    private async void OnSuspend(object sender, RoutedEventArgs e) => await UpdateStatus(LicenseStatus.Suspended);

    private async Task UpdateStatus(LicenseStatus status)
    {
        if (_selectedLicense is null) return;
        _selectedLicense.Status = status;
        StatusBox.SelectedItem = status;
        await EditorStore.SaveAsync(_data);
    }

    private async void OnExtend(object sender, RoutedEventArgs e)
    {
        if (_selectedLicense is null) return;
        _selectedLicense.ExpiresAt = DateTimeOffset.UtcNow.AddYears(1);
        EndDate.SelectedDate = _selectedLicense.ExpiresAt.Date;
        await EditorStore.SaveAsync(_data);
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        if (_selectedTenant is null || _selectedLicense is null) return;
        try
        {
            var privateKey = await EditorStore.EnsurePrivateKeyAsync();
            var payload = new RaqmiLicensePayload
            {
                LicenseId = _selectedLicense.Id,
                TenantId = _selectedTenant.Id,
                TenantName = _selectedTenant.Name,
                Kind = _selectedLicense.Kind,
                Mode = _selectedLicense.Mode,
                Status = _selectedLicense.Status,
                StartsAt = _selectedLicense.StartsAt,
                ExpiresAt = _selectedLicense.ExpiresAt,
                AllowedModules = _selectedLicense.AllowedModules,
                Limits = _selectedLicense.Limits,
                ServerFingerprint = string.IsNullOrWhiteSpace(FingerprintBox.Text) ? null : FingerprintBox.Text.Trim(),
                IssuedAt = DateTimeOffset.UtcNow,
            };

            var file = await LicenseFileService.SignAsync(payload, privateKey);
            var dialog = new SaveFileDialog
            {
                FileName = $"{_selectedTenant.Name.Replace(' ', '_')}.license",
                Filter = "Raqmi License|*.license;*.json",
            };

            if (dialog.ShowDialog() != true) return;
            await File.WriteAllTextAsync(dialog.FileName, LicenseFileService.Serialize(file));
            ShowStatus($"Licence exportée : {dialog.FileName}", success: true);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, success: false);
        }
    }

    private void ShowStatus(string message, bool success)
    {
        StatusPanel.Visibility = Visibility.Visible;
        StatusMessage.Text = message;
        StatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(
            success ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#86EFAC")!
                    : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5")!);
    }
}
