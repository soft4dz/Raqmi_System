using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Core;

public partial class SiteListView : UserControl
{
    private readonly BusinessApiClient _api;
    private List<SiteDto> _sites = [];
    private string? _editingId;

    private static readonly (string Value, string Label)[] SiteTypes =
    [
        ("hotel", "Hôtel"),
        ("annexe", "Annexe"),
        ("agency", "Agence"),
        ("branch", "Succursale"),
        ("site", "Site"),
    ];

    public SiteListView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        InitTypeCombo(TypeBox);
        InitTypeCombo(EditTypeBox);
        Loaded += async (_, _) => await LoadAsync();
    }

    private static void InitTypeCombo(ComboBox combo)
    {
        combo.ItemsSource = SiteTypes.Select(t => new { t.Value, t.Label }).ToList();
        combo.DisplayMemberPath = "Label";
        combo.SelectedValuePath = "Value";
        combo.SelectedIndex = 0;
    }

    private async Task LoadAsync()
    {
        HideError();
        try
        {
            var data = await _api.GetSitesAsync();
            _sites = data.Items;
            Grid.ItemsSource = _sites.Select(s => new SiteRowVm
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Type = s.Type ?? "site",
                City = s.City ?? "—",
                Active = s.Active,
                StatusLabel = s.Active ? "Actif" : "Inactif",
            }).ToList();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        HideError();
        if (string.IsNullOrWhiteSpace(CodeBox.Text) || string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ShowError("Code et nom requis");
            return;
        }

        try
        {
            await _api.CreateSiteAsync(new
            {
                code = CodeBox.Text.Trim(),
                name = NameBox.Text.Trim(),
                type = TypeBox.SelectedValue?.ToString() ?? "site",
                city = string.IsNullOrWhiteSpace(CityBox.Text) ? null : CityBox.Text.Trim(),
            });
            CodeBox.Text = string.Empty;
            NameBox.Text = string.Empty;
            CityBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var site = _sites.FirstOrDefault(s => s.Id == id);
        if (site is null) return;

        _editingId = id;
        EditTitle.Text = $"Modifier — {site.Code}";
        EditNameBox.Text = site.Name;
        EditCityBox.Text = site.City ?? string.Empty;
        EditTypeBox.SelectedValue = site.Type ?? "site";
        EditPanel.Visibility = Visibility.Visible;
    }

    private async void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (_editingId is null) return;
        try
        {
            await _api.UpdateSiteAsync(_editingId, new
            {
                name = EditNameBox.Text.Trim(),
                type = EditTypeBox.SelectedValue?.ToString() ?? "site",
                city = EditCityBox.Text.Trim(),
            });
            EditPanel.Visibility = Visibility.Collapsed;
            _editingId = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e)
    {
        EditPanel.Visibility = Visibility.Collapsed;
        _editingId = null;
    }

    private async void OnToggleActive(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var site = _sites.FirstOrDefault(s => s.Id == id);
        if (site is null) return;

        try
        {
            await _api.UpdateSiteAsync(id, new { active = !site.Active });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    private sealed class SiteRowVm
    {
        public required string Id { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string City { get; init; }
        public required bool Active { get; init; }
        public required string StatusLabel { get; init; }
    }
}
