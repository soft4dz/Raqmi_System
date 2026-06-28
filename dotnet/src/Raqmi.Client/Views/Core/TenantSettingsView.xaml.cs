using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Raqmi.Client.Views.Core;

public partial class TenantSettingsView : UserControl
{
    private readonly BusinessApiClient _api;

    public TenantSettingsView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        HideMessages();
        try
        {
            var s = await _api.GetTenantSettingsAsync();
            LegalNameBox.Text = s.LegalName ?? string.Empty;
            EmailBox.Text = s.Email ?? string.Empty;
            PhoneBox.Text = s.Phone ?? string.Empty;
            AddressBox.Text = s.Address ?? string.Empty;
            CurrencyBox.Text = s.Currency;
            TimezoneBox.Text = s.Timezone;
            DateFormatBox.Text = s.DateFormat;
            NumberFormatBox.Text = s.NumberFormat;
            PaymentDelayBox.Text = s.PaymentDelayDays.ToString(CultureInfo.InvariantCulture);
            ReminderDelayBox.Text = s.ReminderDelayDays.ToString(CultureInfo.InvariantCulture);
            BrandColorBox.Text = s.BrandPrimaryColor ?? "#2563eb";
            BrandLogoBox.Text = s.BrandLogoUrl ?? string.Empty;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        HideMessages();
        if (!int.TryParse(PaymentDelayBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var paymentDelay) ||
            !int.TryParse(ReminderDelayBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reminderDelay))
        {
            ShowError("Délais invalides");
            return;
        }

        try
        {
            await _api.UpdateTenantSettingsAsync(new
            {
                legalName = LegalNameBox.Text.Trim(),
                email = EmailBox.Text.Trim(),
                phone = PhoneBox.Text.Trim(),
                address = AddressBox.Text.Trim(),
                currency = CurrencyBox.Text.Trim(),
                timezone = TimezoneBox.Text.Trim(),
                dateFormat = DateFormatBox.Text.Trim(),
                numberFormat = NumberFormatBox.Text.Trim(),
                paymentDelayDays = paymentDelay,
                reminderDelayDays = reminderDelay,
                brandPrimaryColor = BrandColorBox.Text.Trim(),
                brandLogoUrl = string.IsNullOrWhiteSpace(BrandLogoBox.Text) ? null : BrandLogoBox.Text.Trim(),
            });
            SuccessText.Text = "Paramètres enregistrés.";
            SuccessText.Visibility = Visibility.Visible;
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

    private void HideMessages()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SuccessText.Visibility = Visibility.Collapsed;
    }
}
