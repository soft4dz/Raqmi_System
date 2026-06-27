using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Hr;

public partial class AttendanceView : UserControl
{
    private readonly BusinessApiClient _api;

    public AttendanceView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetAttendanceAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmployeeIdBox.Text)) return;
        await _api.CreateAttendanceAsync(new { employeeId = EmployeeIdBox.Text, notes = NotesBox.Text });
        await RefreshAsync();
    }
}
