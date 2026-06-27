using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Hr;

public partial class EmployeeListView : UserControl
{
    private readonly BusinessApiClient _api;

    public EmployeeListView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetEmployeesAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        await _api.CreateEmployeeAsync(new
        {
            matricule = MatriculeBox.Text,
            firstName = FirstNameBox.Text,
            lastName = LastNameBox.Text,
            department = "general",
        });
        MatriculeBox.Text = string.Empty;
        FirstNameBox.Text = string.Empty;
        LastNameBox.Text = string.Empty;
        await RefreshAsync();
    }
}
