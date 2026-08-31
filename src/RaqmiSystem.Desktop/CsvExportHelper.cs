using System.Globalization;
using System.IO;
using System.Text;
using RaqmiSystem.Application.Reporting;
using RaqmiSystem.Application.Revenue;
using RaqmiSystem.Application.Security;

namespace RaqmiSystem.Desktop;

/// <summary>
/// Builds CSV text from rows already loaded in memory (as displayed in a DataGrid) and
/// writes it to disk. No network call and no server-side export endpoint is involved.
/// </summary>
public static class CsvExportHelper
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static string BuildDailyRevenueCsv(IEnumerable<DailyRevenueResponse> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, "Date", "Unité", "Hébergement", "Restauration", "Boissons", "Autres", "Total", "Statut", "Saisi par");

        foreach (var row in rows)
        {
            // Amounts are deliberately invariant-culture, no thousands separator (e.g.
            // "1234.56", not "1,234.56" or the current-culture display format): this keeps
            // the CSV a portable, machine-readable format for re-import into other tools,
            // distinct from the grouped/localized formatting used on screen and in print.
            AppendRow(
                builder,
                row.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.HotelUnitCode,
                row.Accommodation.ToString("F2", CultureInfo.InvariantCulture),
                row.Food.ToString("F2", CultureInfo.InvariantCulture),
                row.Beverage.ToString("F2", CultureInfo.InvariantCulture),
                row.Other.ToString("F2", CultureInfo.InvariantCulture),
                row.Total.ToString("F2", CultureInfo.InvariantCulture),
                DailyRevenueStatusDisplay.ToFrench(row.Status),
                row.CreatedBy);
        }

        return builder.ToString();
    }

    public static string BuildAuditLogCsv(IEnumerable<AuditLogSummary> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, "Date/heure", "Utilisateur", "Action", "Entité", "Id entité", "Adresse IP");

        foreach (var row in rows)
        {
            AppendRow(
                builder,
                row.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                row.UserName ?? string.Empty,
                row.Action,
                row.EntityName,
                row.EntityId ?? string.Empty,
                row.IpAddress ?? string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generic export for the reporting module: the header row comes from the report's column
    /// labels and every cell keeps the RAW invariant value returned by the server (dates
    /// yyyy-MM-dd, amounts with a dot and no thousands separator) - machine format, distinct
    /// from the localized display formatting the grid applies, exactly like the other exports
    /// of this helper. The total row, when present, is exported last.
    /// </summary>
    public static string BuildReportCsv(ReportResultResponse result)
    {
        var builder = new StringBuilder();

        AppendRow(builder, result.Columns.Select(column => column.Label).ToArray());

        foreach (var row in result.Rows)
        {
            AppendRow(builder, row.Select(cell => cell ?? string.Empty).ToArray());
        }

        if (result.TotalRow is not null)
        {
            AppendRow(builder, result.TotalRow.Select(cell => cell ?? string.Empty).ToArray());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes CSV text as UTF-8 with a byte-order mark, so Excel on Windows renders
    /// accented French characters correctly (without the BOM it often mis-reads them).
    /// </summary>
    public static void WriteCsvFile(string path, string csvContent)
    {
        File.WriteAllText(path, csvContent, Utf8WithBom);
    }

    private static void AppendRow(StringBuilder builder, params string[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeField(fields[i]));
        }

        builder.Append("\r\n");
    }

    private static readonly char[] FormulaTriggerCharacters = ['=', '+', '-', '@'];

    private static string EscapeField(string? value)
    {
        value ??= string.Empty;

        // CSV formula injection (OWASP): a field opened by Excel/LibreOffice/Sheets that
        // starts with =, +, - or @ can be interpreted as a formula rather than plain text.
        // Prefixing with an apostrophe forces spreadsheet applications to treat it as text.
        if (value.Length > 0 && FormulaTriggerCharacters.Contains(value[0]))
        {
            value = "'" + value;
        }

        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');

        if (!needsQuoting)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
