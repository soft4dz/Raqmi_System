using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RaqmiSystem.Desktop.Views;

public partial class PosView : UserControl
{
    private readonly ObservableCollection<PosLine> lines = [];
    private int ticketSequence = 1;

    public PosView()
    {
        InitializeComponent();
        TicketGrid.ItemsSource = lines;
    }

    private void Product_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var parts = tag.Split('|');
        if (parts.Length != 2 || !decimal.TryParse(parts[1], CultureInfo.InvariantCulture, out var price)) return;
        var existing = lines.FirstOrDefault(line => line.Name == parts[0]);
        if (existing is null) lines.Add(new PosLine(parts[0], 1, price));
        else { var index = lines.IndexOf(existing); lines[index] = existing with { Quantity = existing.Quantity + 1 }; }
        RefreshTotal();
    }

    private void NewTicket_Click(object sender, RoutedEventArgs e) => ResetTicket("Nouveau ticket prêt");

    private void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (lines.Count == 0) { PosStatusText.Text = "Ajoutez au moins un article."; return; }
        var method = (sender as Button)?.Tag?.ToString() ?? "Paiement";
        var total = lines.Sum(line => line.Total);
        ResetTicket($"{method} enregistré localement : {total:N0} DA");
    }

    private void ResetTicket(string status)
    {
        lines.Clear();
        ticketSequence++;
        TicketNumberText.Text = $"#LOCAL-{ticketSequence:000}";
        PosStatusText.Text = status;
        RefreshTotal();
    }

    private void RefreshTotal() => TotalText.Text = $"{lines.Sum(line => line.Total):N0} DA";

    private sealed record PosLine(string Name, int Quantity, decimal UnitPrice)
    {
        public decimal Total => Quantity * UnitPrice;
    }
}
