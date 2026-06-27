using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Stocks;

public partial class ProductListView : UserControl
{
    private readonly BusinessApiClient _api;

    public ProductListView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetProductsAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        await _api.CreateProductAsync(new { code = CodeBox.Text, name = NameBox.Text, unit = "u" });
        CodeBox.Text = string.Empty;
        NameBox.Text = string.Empty;
        await RefreshAsync();
    }
}
