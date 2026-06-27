using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Stocks;

public partial class InventoryView : UserControl
{
    private readonly BusinessApiClient _api;

    public InventoryView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetInventoriesAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        await _api.CreateInventoryAsync(new { });
        await RefreshAsync();
    }
}
