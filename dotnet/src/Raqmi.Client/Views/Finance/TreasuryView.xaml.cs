using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Finance;

public partial class TreasuryView : UserControl
{
    private readonly BusinessApiClient _api;

    public TreasuryView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetTreasuryAsync();
        BalanceText.Text = data.Balance.ToString("N2", CultureInfo.CurrentCulture);
        Grid.ItemsSource = data.Items;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) return;
        await _api.CreateTreasuryMovementAsync(new { type = TypeBox.Text, amount, label = LabelBox.Text, account = "cash" });
        await RefreshAsync();
    }
}
