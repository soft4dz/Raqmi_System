using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Raqmi.Client.Views.Ged;

public partial class DocumentLibraryView : UserControl
{
    private readonly BusinessApiClient _api;

    public DocumentLibraryView(BusinessApiClient api)
    {
        _api = api;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var data = await _api.GetDocumentsAsync();
        Grid.ItemsSource = data.Items;
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void OnUpload(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog();
        if (dialog.ShowDialog() != true) return;
        await _api.UploadDocumentAsync(dialog.FileName);
        await RefreshAsync();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        await _api.DeleteDocumentAsync(id);
        await RefreshAsync();
    }
}
