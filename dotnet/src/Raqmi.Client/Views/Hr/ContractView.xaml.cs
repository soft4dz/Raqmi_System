using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Hr;

public partial class ContractView : UserControl
{
    private readonly BusinessApiClient _api;

    public ContractView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await LoadEmployeesAsync();
        EmployeeBox.SelectionChanged += async (_, _) => await RefreshContractsAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        var data = await _api.GetEmployeesAsync();
        EmployeeBox.ItemsSource = data.Items.Select(e => new EmployeeOption(e.Id, $"{e.FirstName} {e.LastName} ({e.Matricule})")).ToList();
        if (EmployeeBox.Items.Count > 0) EmployeeBox.SelectedIndex = 0;
    }

    private async Task RefreshContractsAsync()
    {
        if (EmployeeBox.SelectedItem is not EmployeeOption employee) return;
        var data = await _api.GetContractsAsync(employee.Id);
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (EmployeeBox.SelectedItem is not EmployeeOption employee) return;
        if (!decimal.TryParse(SalaryBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var salary)) return;
        await _api.CreateContractAsync(employee.Id, new { contractType = TypeBox.Text, baseSalary = salary });
        await RefreshContractsAsync();
    }

    private sealed record EmployeeOption(string Id, string Label);
}
