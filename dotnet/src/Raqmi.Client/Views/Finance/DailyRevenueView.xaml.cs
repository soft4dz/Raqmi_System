using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Finance;

public partial class DailyRevenueView : UserControl
{
    private readonly BusinessApiClient _api;

    public DailyRevenueView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var data = await _api.GetDailyRevenueAsync();
            Grid.ItemsSource = data.Items;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        HideError();
        if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            ShowError("Montant invalide");
            return;
        }

        try
        {
            await _api.CreateDailyRevenueAsync(new { amount, category = CategoryBox.Text, notes = NotesBox.Text });
            AmountBox.Text = "0";
            NotesBox.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnValidate(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        try
        {
            await _api.ValidateDailyRevenueAsync(id);
            await RefreshAsync();
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
}
