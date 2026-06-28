using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Admin;

public partial class AuditLogView : UserControl
{
    private readonly BusinessApiClient _api;
    private List<UserDto> _users = [];

    private static readonly (string Value, string Label)[] ActionOptions =
    [
        ("", "Toutes"),
        ("login", "login"),
        ("create", "create"),
        ("update", "update"),
        ("delete", "delete"),
    ];

    public AuditLogView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        ActionBox.ItemsSource = ActionOptions.Select(a => new { a.Value, a.Label }).ToList();
        ActionBox.DisplayMemberPath = "Label";
        ActionBox.SelectedValuePath = "Value";
        ActionBox.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        HideError();
        try
        {
            var action = ActionBox.SelectedValue?.ToString();
            var moduleCode = ModuleBox.Text.Trim();
            var q = SearchBox.Text.Trim();

            var logsTask = _api.GetAuditLogsAsync(
                string.IsNullOrWhiteSpace(action) ? null : action,
                string.IsNullOrWhiteSpace(moduleCode) ? null : moduleCode,
                string.IsNullOrWhiteSpace(q) ? null : q);
            var usersTask = _api.GetUsersAsync();
            await Task.WhenAll(logsTask, usersTask);

            _users = usersTask.Result.Items;
            var userMap = _users.ToDictionary(u => u.Id, u => u.FullName);

            Grid.ItemsSource = logsTask.Result.Items.Select(log => new AuditRowVm
            {
                Id = log.Id,
                CreatedAtLabel = log.CreatedAt.ToLocalTime().ToString("g"),
                UserName = log.UserId is not null && userMap.TryGetValue(log.UserId, out var name) ? name : log.UserId ?? "—",
                Action = log.Action,
                ModuleCode = log.ModuleCode ?? "—",
                Description = log.Description,
            }).ToList();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnFilter(object sender, RoutedEventArgs e) => await LoadAsync();

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    private sealed class AuditRowVm
    {
        public required string Id { get; init; }
        public required string CreatedAtLabel { get; init; }
        public required string UserName { get; init; }
        public required string Action { get; init; }
        public required string ModuleCode { get; init; }
        public required string Description { get; init; }
    }
}
