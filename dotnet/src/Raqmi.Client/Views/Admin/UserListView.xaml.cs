using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Admin;

public partial class UserListView : UserControl
{
    private readonly BusinessApiClient _api;
    private List<UserDto> _users = [];
    private List<SiteDto> _sites = [];
    private readonly HashSet<string> _selectedSiteIds = [];
    private readonly HashSet<string> _editSiteIds = [];
    private string? _editingUserId;
    private string? _editingProfileUserId;

    public UserListView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        HideError();
        try
        {
            var roles = await _api.GetRolesAsync();
            RoleBox.ItemsSource = roles.Items;
            RoleBox.DisplayMemberPath = nameof(RoleDetailDto.Label);
            RoleBox.SelectedValuePath = nameof(RoleDetailDto.Code);
            ProfileRoleBox.ItemsSource = roles.Items;
            ProfileRoleBox.DisplayMemberPath = nameof(RoleDetailDto.Label);
            ProfileRoleBox.SelectedValuePath = nameof(RoleDetailDto.Code);
            if (RoleBox.Items.Count > 0) RoleBox.SelectedIndex = 0;

            var sitesRes = await _api.GetAdminSitesAsync();
            _sites = sitesRes.Items;
            if (_selectedSiteIds.Count == 0 && _sites.Count > 0)
                _selectedSiteIds.Add(_sites[0].Id);
            RenderSitePicker(SitesPicker, _selectedSiteIds, isEdit: false);

            var data = await _api.GetUsersAsync();
            _users = data.Items;
            Grid.ItemsSource = _users.Select(u => new UserRowVm
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                RoleCode = u.RoleCode,
                Active = u.Active,
                SitesLabel = FormatSites(u.SiteIds),
            }).ToList();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private string FormatSites(IReadOnlyList<string> siteIds) =>
        string.Join(", ", siteIds.Select(id => _sites.FirstOrDefault(s => s.Id == id)?.Name ?? id));

    private void RenderSitePicker(ItemsControl host, HashSet<string> selected, bool isEdit)
    {
        host.Items.Clear();
        foreach (var site in _sites)
        {
            var check = new CheckBox
            {
                Content = $"{site.Name}{(string.IsNullOrWhiteSpace(site.City) ? "" : $" ({site.City})")}",
                IsChecked = selected.Contains(site.Id),
                Tag = site.Id,
                Margin = new Thickness(0, 0, 0, 4),
            };
            check.Checked += (_, _) => selected.Add(site.Id);
            check.Unchecked += (_, _) => selected.Remove(site.Id);
            host.Items.Add(check);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        HideError();
        if (_selectedSiteIds.Count == 0)
        {
            ShowError("Sélectionnez au moins un site");
            return;
        }

        try
        {
            await _api.CreateUserAsync(new
            {
                email = EmailBox.Text,
                fullName = FullNameBox.Text,
                roleCode = RoleBox.SelectedValue?.ToString() ?? "user",
                siteIds = _selectedSiteIds.ToArray(),
            });
            EmailBox.Text = string.Empty;
            FullNameBox.Text = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnEditSites(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user is null) return;

        EditProfilePanel.Visibility = Visibility.Collapsed;
        _editingProfileUserId = null;
        _editingUserId = id;
        _editSiteIds.Clear();
        foreach (var siteId in user.SiteIds) _editSiteIds.Add(siteId);
        EditSitesTitle.Text = $"Sites — {user.FullName}";
        RenderSitePicker(EditSitesPicker, _editSiteIds, isEdit: true);
        EditSitesPanel.Visibility = Visibility.Visible;
    }

    private async void OnSaveSites(object sender, RoutedEventArgs e)
    {
        if (_editingUserId is null || _editSiteIds.Count == 0)
        {
            ShowError("Sélectionnez au moins un site");
            return;
        }

        try
        {
            await _api.UpdateUserAsync(_editingUserId, new { siteIds = _editSiteIds.ToArray() });
            EditSitesPanel.Visibility = Visibility.Collapsed;
            _editingUserId = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnCancelEditSites(object sender, RoutedEventArgs e)
    {
        EditSitesPanel.Visibility = Visibility.Collapsed;
        _editingUserId = null;
    }

    private void OnEditProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user is null) return;

        EditSitesPanel.Visibility = Visibility.Collapsed;
        _editingUserId = null;
        _editingProfileUserId = id;
        EditProfileTitle.Text = $"Profil — {user.Email}";
        ProfileFullNameBox.Text = user.FullName;
        ProfileRoleBox.SelectedValue = user.RoleCode;
        ProfilePasswordBox.Password = string.Empty;
        EditProfilePanel.Visibility = Visibility.Visible;
    }

    private async void OnSaveProfile(object sender, RoutedEventArgs e)
    {
        if (_editingProfileUserId is null) return;

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["fullName"] = ProfileFullNameBox.Text.Trim(),
                ["roleCode"] = ProfileRoleBox.SelectedValue?.ToString() ?? "user",
            };
            if (!string.IsNullOrWhiteSpace(ProfilePasswordBox.Password))
                body["password"] = ProfilePasswordBox.Password;

            await _api.UpdateUserAsync(_editingProfileUserId, body);
            EditProfilePanel.Visibility = Visibility.Collapsed;
            _editingProfileUserId = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnCancelProfile(object sender, RoutedEventArgs e)
    {
        EditProfilePanel.Visibility = Visibility.Collapsed;
        _editingProfileUserId = null;
    }

    private async void OnToggleActive(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user is null) return;

        try
        {
            await _api.UpdateUserAsync(id, new { active = !user.Active });
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

    private sealed class UserRowVm
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FullName { get; init; }
        public required string RoleCode { get; init; }
        public required bool Active { get; init; }
        public required string SitesLabel { get; init; }
    }
}
