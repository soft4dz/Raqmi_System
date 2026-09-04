using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RaqmiSystem.Application.Organization;
using RaqmiSystem.Application.Pos;
using RaqmiSystem.Domain.Pos;

namespace RaqmiSystem.Desktop.Views;

public partial class PosView : UserControl
{
    private ModuleViewContext? context;
    private readonly ObservableCollection<CartLine> cart=[];
    private Guid? editingOutletId,editingProductId,editingTableId;
    private bool changingSelection;
    public PosView(){InitializeComponent();CartGrid.ItemsSource=cart;}
    public void Initialize(ModuleViewContext value){context=value;}
    public void ResetState(){context=null;UnitCombo.ItemsSource=null;OutletCombo.ItemsSource=null;OutletsGrid.ItemsSource=null;ProductsGrid.ItemsSource=null;TablesGrid.ItemsSource=null;TicketsGrid.ItemsSource=null;cart.Clear();RefreshCart();}
    public async Task LoadAsync(){var c=context;if(c is null)return;await c.RunAsync(async()=>{changingSelection=true;var units=await c.ApiClient.GetHotelUnitsAsync(c.ApiBaseUrl,false);UnitCombo.ItemsSource=units;UnitCombo.SelectedIndex=units.Count>0?0:-1;changingSelection=false;await LoadUnitAsync();});}
    private HotelUnitResponse? Unit=>UnitCombo.SelectedItem as HotelUnitResponse;
    private PosOutletResponse? Outlet=>OutletCombo.SelectedItem as PosOutletResponse;
    private async Task LoadUnitAsync(){var c=context;var unit=Unit;if(c is null||unit is null)return;var outlets=await c.ApiClient.GetPosOutletsAsync(c.ApiBaseUrl,unit.Code,true);changingSelection=true;OutletCombo.ItemsSource=outlets.Where(x=>x.IsActive).ToArray();OutletCombo.SelectedIndex=outlets.Count>0?0:-1;OutletsGrid.ItemsSource=outlets;changingSelection=false;await LoadOutletAsync();var d=await c.ApiClient.GetPosDashboardAsync(c.ApiBaseUrl,unit.Code,DateOnly.FromDateTime(DateTime.Today));RevenueText.Text=$"{d.Revenue:N0} DA";PaidCountText.Text=d.PaidTickets.ToString();OpenCountText.Text=d.OpenTickets.ToString();AverageText.Text=$"{d.AverageTicket:N0} DA";OutletSalesGrid.ItemsSource=d.ByOutlet;}
    private async Task LoadOutletAsync(){var c=context;var outlet=Outlet;if(c is null||outlet is null){ProductsGrid.ItemsSource=null;ProductList.ItemsSource=null;TablesGrid.ItemsSource=null;TicketsGrid.ItemsSource=null;return;}var products=await c.ApiClient.GetPosProductsAsync(c.ApiBaseUrl,outlet.Id,true);ProductsGrid.ItemsSource=products;ProductList.ItemsSource=products.Where(x=>x.IsActive).ToArray();TablesGrid.ItemsSource=await c.ApiClient.GetPosTablesAsync(c.ApiBaseUrl,outlet.Id);TicketsGrid.ItemsSource=await c.ApiClient.GetPosTicketsAsync(c.ApiBaseUrl,outlet.Id,DateOnly.FromDateTime(DateTime.Today));}
    private async void UnitChanged(object s,SelectionChangedEventArgs e){if(changingSelection||context is null)return;await context.RunAsync(LoadUnitAsync);}
    private async void OutletChanged(object s,SelectionChangedEventArgs e){if(changingSelection||context is null)return;cart.Clear();RefreshCart();await context.RunAsync(LoadOutletAsync);}
    private async void Refresh_Click(object s,RoutedEventArgs e){if(context is not null)await context.RunAsync(LoadUnitAsync);}
    private void ProductDoubleClick(object s,MouseButtonEventArgs e){if(ProductList.SelectedItem is not PosProductResponse p)return;var old=cart.FirstOrDefault(x=>x.ProductId==p.Id);if(old is null)cart.Add(new(p.Id,p.Name,1,p.Price));else{var i=cart.IndexOf(old);cart[i]=old with{Quantity=old.Quantity+1};}RefreshCart();}
    private void ClearCart_Click(object s,RoutedEventArgs e){cart.Clear();RefreshCart();}
    private async void Pay_Click(object s,RoutedEventArgs e){var c=context;var outlet=Outlet;if(c is null||outlet is null)return;if(cart.Count==0){c.SetStatus("Ajoutez au moins un article.",true);return;}if(!Enum.TryParse<PosPaymentMethod>((s as Button)?.Tag?.ToString(),out var method))return;await c.RunAsync(async()=>{var request=new CreatePosTicketRequest(outlet.Id,method==PosPaymentMethod.RoomCharge?PosOrderType.RoomService:PosOrderType.DineIn,null,null,cart.Select(x=>new PosTicketLineRequest(x.ProductId,x.Quantity)).ToArray());var ticket=await c.ApiClient.CreatePosTicketAsync(c.ApiBaseUrl,request);await c.ApiClient.PayPosTicketAsync(c.ApiBaseUrl,ticket.Id,method);cart.Clear();RefreshCart();await LoadUnitAsync();c.SetStatus($"Ticket {ticket.Number} encaissé.");});}
    private async void SaveOutlet_Click(object s,RoutedEventArgs e){var c=context;var unit=Unit;if(c is null||unit is null)return;var kind=(OutletKindCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()??"Restaurant";await c.RunAsync(async()=>{await c.ApiClient.SavePosOutletAsync(c.ApiBaseUrl,editingOutletId,new(OutletCodeBox.Text,OutletNameBox.Text,unit.Code,kind,OutletActiveCheck.IsChecked==true));NewOutlet();await LoadUnitAsync();c.SetStatus("Point de vente enregistré.");});}
    private void OutletGridChanged(object s,SelectionChangedEventArgs e){if(OutletsGrid.SelectedItem is not PosOutletResponse x)return;editingOutletId=x.Id;OutletCodeBox.Text=x.Code;OutletCodeBox.IsEnabled=false;OutletNameBox.Text=x.Name;OutletActiveCheck.IsChecked=x.IsActive;SelectKind(x.Kind);}
    private void NewOutlet_Click(object s,RoutedEventArgs e)=>NewOutlet();
    private void NewOutlet(){editingOutletId=null;OutletCodeBox.Clear();OutletCodeBox.IsEnabled=true;OutletNameBox.Clear();OutletKindCombo.SelectedIndex=0;OutletActiveCheck.IsChecked=true;OutletsGrid.SelectedItem=null;}
    private async void SaveProduct_Click(object s,RoutedEventArgs e){var c=context;var outlet=Outlet;if(c is null||outlet is null)return;if(!decimal.TryParse(ProductPriceBox.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out var price)){c.SetStatus("Prix invalide.",true);return;}await c.RunAsync(async()=>{await c.ApiClient.SavePosProductAsync(c.ApiBaseUrl,outlet.Id,editingProductId,new(ProductCodeBox.Text,ProductNameBox.Text,ProductCategoryBox.Text,price,ProductActiveCheck.IsChecked==true));NewProduct();await LoadOutletAsync();c.SetStatus("Article POS enregistré.");});}
    private void ProductGridChanged(object s,SelectionChangedEventArgs e){if(ProductsGrid.SelectedItem is not PosProductResponse x)return;editingProductId=x.Id;ProductCodeBox.Text=x.Code;ProductCodeBox.IsEnabled=false;ProductNameBox.Text=x.Name;ProductCategoryBox.Text=x.Category;ProductPriceBox.Text=x.Price.ToString("0.##",CultureInfo.CurrentCulture);ProductActiveCheck.IsChecked=x.IsActive;}
    private void NewProduct_Click(object s,RoutedEventArgs e)=>NewProduct();
    private void NewProduct(){editingProductId=null;ProductCodeBox.Clear();ProductCodeBox.IsEnabled=true;ProductNameBox.Clear();ProductCategoryBox.Clear();ProductPriceBox.Clear();ProductActiveCheck.IsChecked=true;ProductsGrid.SelectedItem=null;}
    private async void SaveTable_Click(object s,RoutedEventArgs e){var c=context;var outlet=Outlet;if(c is null||outlet is null)return;if(!int.TryParse(TableSeatsBox.Text,out var seats)){c.SetStatus("Nombre de places invalide.",true);return;}await c.RunAsync(async()=>{await c.ApiClient.SavePosTableAsync(c.ApiBaseUrl,outlet.Id,editingTableId,new(TableZoneBox.Text,TableNumberBox.Text,seats,TableActiveCheck.IsChecked==true));NewTable();await LoadOutletAsync();c.SetStatus("Table enregistrée.");});}
    private void TableGridChanged(object s,SelectionChangedEventArgs e){if(TablesGrid.SelectedItem is not PosTableResponse x)return;editingTableId=x.Id;TableZoneBox.Text=x.Zone;TableNumberBox.Text=x.Number;TableSeatsBox.Text=x.Seats.ToString();TableActiveCheck.IsChecked=x.IsActive;}
    private void NewTable_Click(object s,RoutedEventArgs e)=>NewTable();
    private void NewTable(){editingTableId=null;TableZoneBox.Clear();TableNumberBox.Clear();TableSeatsBox.Text="4";TableActiveCheck.IsChecked=true;TablesGrid.SelectedItem=null;}
    private void SelectKind(string kind){foreach(var item in OutletKindCombo.Items.OfType<ComboBoxItem>())if(string.Equals(item.Content?.ToString(),kind,StringComparison.OrdinalIgnoreCase)){OutletKindCombo.SelectedItem=item;return;}OutletKindCombo.SelectedIndex=0;}
    private void RefreshCart(){CartTotalText.Text=$"{cart.Sum(x=>x.Total):N0} DA";}
    private sealed record CartLine(Guid ProductId,string Name,int Quantity,decimal UnitPrice){public decimal Total=>Quantity*UnitPrice;}
}
