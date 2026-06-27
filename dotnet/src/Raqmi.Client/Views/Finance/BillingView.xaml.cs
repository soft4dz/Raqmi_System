using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Finance;

public partial class BillingView : UserControl
{
    private readonly BusinessApiClient _api;

    public BillingView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var data = await _api.GetInvoicesAsync();
            InvoiceGrid.ItemsSource = data.Items;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        HideError();
        try
        {
            await _api.CreateInvoiceAsync(new
            {
                clientName = ClientBox.Text,
                lines = new[] { new { description = "Prestation", quantity = 1m, unitPrice = 100m, taxRate = 0.2m } },
            });
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnInvoiceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (InvoiceGrid.SelectedItem is not InvoiceDto invoice)
        {
            DetailHeader.Text = string.Empty;
            LinesGrid.ItemsSource = null;
            return;
        }

        DetailHeader.Text = $"{invoice.Number} — {invoice.ClientName} ({invoice.Status})";
        LinesGrid.ItemsSource = invoice.Lines ?? [];
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;
}
