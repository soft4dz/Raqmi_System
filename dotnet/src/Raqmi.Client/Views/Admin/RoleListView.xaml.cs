using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Admin;

public partial class RoleListView : UserControl
{
    private readonly BusinessApiClient _api;
    private List<RoleDetailDto> _roles = [];
    private List<PermissionDto> _permissions = [];
    private readonly HashSet<string> _createPermissions = ["finance:read"];
    private readonly HashSet<string> _editPermissions = [];
    private string? _editingCode;
    private bool _editingIsSystem;

    public RoleListView(BusinessApiClient api)
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
            var rolesRes = await _api.GetRolesAsync();
            var permsRes = await _api.GetPermissionsAsync();
            _roles = rolesRes.Items;
            _permissions = permsRes.Items;

            RenderPermissionPicker(CreatePermissionsPicker, _createPermissions);
            Grid.ItemsSource = _roles.Select(r => new RoleRowVm
            {
                Code = r.Code,
                Label = r.Label,
                TypeLabel = r.IsSystem ? "Système" : "Personnalisé",
                PermissionsLabel = string.Join(", ", r.Permissions),
                IsSystem = r.IsSystem,
            }).ToList();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void RenderPermissionPicker(ItemsControl host, HashSet<string> selected)
    {
        host.Items.Clear();
        foreach (var perm in _permissions)
        {
            var check = new CheckBox
            {
                Content = perm.Label,
                IsChecked = selected.Contains(perm.Key),
                Tag = perm.Key,
                Margin = new Thickness(0, 0, 0, 4),
            };
            check.Checked += (_, _) => selected.Add(perm.Key);
            check.Unchecked += (_, _) => selected.Remove(perm.Key);
            host.Items.Add(check);
        }
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        HideError();
        if (string.IsNullOrWhiteSpace(CodeBox.Text) || string.IsNullOrWhiteSpace(LabelBox.Text))
        {
            ShowError("Code et libellé requis");
            return;
        }

        try
        {
            await _api.CreateRoleAsync(new
            {
                code = CodeBox.Text.Trim(),
                label = LabelBox.Text.Trim(),
                permissions = _createPermissions.ToArray(),
            });
            CodeBox.Text = string.Empty;
            LabelBox.Text = string.Empty;
            _createPermissions.Clear();
            _createPermissions.Add("finance:read");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string code) return;
        var role = _roles.FirstOrDefault(r => r.Code == code);
        if (role is null) return;

        _editingCode = code;
        _editingIsSystem = role.IsSystem;
        EditTitle.Text = $"Modifier — {role.Code}";
        EditLabelBox.Text = role.Label;

        _editPermissions.Clear();
        if (role.Permissions.Contains("*"))
        {
            foreach (var p in _permissions) _editPermissions.Add(p.Key);
        }
        else
        {
            foreach (var p in role.Permissions) _editPermissions.Add(p);
        }

        EditPermissionsPicker.Visibility = role.IsSystem ? Visibility.Collapsed : Visibility.Visible;
        EditPermissionsLabel.Visibility = role.IsSystem ? Visibility.Collapsed : Visibility.Visible;
        SystemRoleHint.Visibility = role.IsSystem ? Visibility.Visible : Visibility.Collapsed;
        if (!role.IsSystem) RenderPermissionPicker(EditPermissionsPicker, _editPermissions);
        EditPanel.Visibility = Visibility.Visible;
    }

    private async void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (_editingCode is null) return;
        try
        {
            if (_editingIsSystem)
            {
                await _api.UpdateRoleAsync(_editingCode, new { label = EditLabelBox.Text.Trim() });
            }
            else
            {
                await _api.UpdateRoleAsync(_editingCode, new
                {
                    label = EditLabelBox.Text.Trim(),
                    permissions = _editPermissions.ToArray(),
                });
            }

            EditPanel.Visibility = Visibility.Collapsed;
            _editingCode = null;
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
        _editingCode = null;
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string code) return;
        var role = _roles.FirstOrDefault(r => r.Code == code);
        if (role is null || role.IsSystem) return;

        try
        {
            await _api.DeleteRoleAsync(code);
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

    private sealed class RoleRowVm
    {
        public required string Code { get; init; }
        public required string Label { get; init; }
        public required string TypeLabel { get; init; }
        public required string PermissionsLabel { get; init; }
        public required bool IsSystem { get; init; }
        public Visibility DeleteVisibility => IsSystem ? Visibility.Collapsed : Visibility.Visible;
    }
}
