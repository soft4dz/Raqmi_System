using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Stocks;

public partial class StockMovementView : UserControl
{
    private readonly BusinessApiClient _api;

    public StockMovementView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetStockMovementsAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(QtyBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)) return;
        await _api.CreateStockMovementAsync(new { productId = ProductIdBox.Text, type = TypeBox.Text, quantity = qty });
        await RefreshAsync();
    }
}
