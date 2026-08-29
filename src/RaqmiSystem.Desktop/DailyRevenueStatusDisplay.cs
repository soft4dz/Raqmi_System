using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Single source of the French display labels for <see cref="DailyRevenueStatus"/>: the
/// DataGrid status badge (MainWindow.xaml), the print document and the CSV export must all
/// render the same wording instead of the raw English enum names.
/// </summary>
public static class DailyRevenueStatusDisplay
{
    public static string ToFrench(DailyRevenueStatus status)
    {
        // Feminine agreement: the labels qualify "une recette".
        return status switch
        {
            DailyRevenueStatus.Draft => "Brouillon",
            DailyRevenueStatus.Submitted => "Soumise",
            DailyRevenueStatus.Validated => "Validée",
            DailyRevenueStatus.Rejected => "Rejetée",
            _ => status.ToString()
        };
    }
}
