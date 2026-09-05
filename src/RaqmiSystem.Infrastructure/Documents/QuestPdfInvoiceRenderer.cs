using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RaqmiSystem.Application.Documents;

namespace RaqmiSystem.Infrastructure.Documents;

/// <summary>
/// Gabarit de la facture (A4, francais), rendu par QuestPDF.
///
/// LICENCE. QuestPDF est utilise sous licence Community : gratuite tant que le chiffre d'affaires
/// annuel de l'organisation qui exploite le logiciel reste inferieur a 1 M$ (condition detaillee
/// dans docs/modules/documents.md). Elle est declaree par <see cref="ConfigureEngine"/>, appele a
/// l'enregistrement du module ET par le constructeur - le moteur refuse de rendre sans elle.
///
/// AUCUN MONTANT N'EST CALCULE ICI : le gabarit imprime le modele. Les seuls traitements sont de
/// mise en forme (separateurs francais, dates jj/mm/aaaa).
/// </summary>
public sealed class QuestPdfInvoiceRenderer : IDocumentRenderer<InvoiceDocumentModel>
{
    /// <summary>A incrementer a chaque changement de mise en page (archive avec chaque document).</summary>
    public const int CurrentTemplateVersion = 1;

    // Format francais explicite plutot que fr-FR de la machine : le separateur de milliers ICU de
    // fr-FR est une espace fine insecable (U+202F) que la police pourrait ne pas couvrir, et le
    // rendu ne doit pas dependre de la culture du serveur.
    private static readonly NumberFormatInfo FrenchAmounts = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
        NumberGroupSizes = [3],
        NumberDecimalDigits = 2,
        NegativeSign = "-"
    };

    private static readonly string HeaderBackground = Colors.Grey.Lighten3;
    private static readonly string RuleColor = Colors.Grey.Darken1;
    private static readonly string LightRuleColor = Colors.Grey.Lighten2;
    private static readonly string MutedText = Colors.Grey.Darken2;

    public QuestPdfInvoiceRenderer()
    {
        ConfigureEngine();
    }

    public int TemplateVersion => CurrentTemplateVersion;

    /// <summary>Declare la licence Community. Idempotent ; appele au demarrage du module.</summary>
    public static void ConfigureEngine()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(InvoiceDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return Compose(model).GeneratePdf();
    }

    /// <summary>
    /// Le document QuestPDF avant generation. Public pour que les tests puissent l'inspecter
    /// (nombre de pages via GenerateImages) sans reparcourir le PDF produit.
    /// </summary>
    public static IDocument Compose(InvoiceDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        // Les dates de metadonnees sont celles de l'emission, pas l'horloge du serveur : deux
        // rendus du meme modele donnent des metadonnees identiques (l'archive rend de toute facon
        // le re-rendu inutile, mais une piece ne doit pas dire qu'elle a ete « creee » a la date
        // ou on l'a reimprimee).
        var stamp = (model.IssuedAt ?? new DateTimeOffset(model.InvoiceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)).UtcDateTime;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(15, Unit.Millimetre);
                page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Lato));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model));
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = $"Facture {model.Number}",
            Author = model.Issuer.Name,
            Subject = $"Facture {model.Number} - {model.Customer.Name}",
            Creator = "Raqmi System",
            Producer = "Raqmi System",
            CreationDate = stamp,
            ModifiedDate = stamp
        });
    }

    private static void ComposeHeader(IContainer container, InvoiceDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(3).Column(issuer =>
                {
                    issuer.Item().Text(model.Issuer.Name).FontSize(14).SemiBold();

                    foreach (var line in AddressLines(model.Issuer))
                    {
                        issuer.Item().Text(line);
                    }

                    foreach (var line in IdentifierLines(model.Issuer))
                    {
                        issuer.Item().Text(line).FontSize(8).FontColor(MutedText);
                    }

                    foreach (var line in ContactLines(model.Issuer))
                    {
                        issuer.Item().Text(line).FontSize(8).FontColor(MutedText);
                    }
                });

                row.RelativeItem(2).Column(title =>
                {
                    title.Item().AlignRight().Text("FACTURE").FontSize(20).Bold();
                    title.Item().AlignRight().Text($"N° {model.Number}").FontSize(11).SemiBold();
                    title.Item().AlignRight().Text($"Date : {FormatDate(model.InvoiceDate)}");

                    if (model.IssuedAt is { } issuedAt)
                    {
                        title.Item().AlignRight().Text($"Émise le {FormatDate(DateOnly.FromDateTime(issuedAt.UtcDateTime))}").FontSize(8).FontColor(MutedText);
                    }

                    title.Item().AlignRight().Text($"Unité : {model.HotelUnitCode}").FontSize(8).FontColor(MutedText);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(RuleColor);

            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem(3);

                row.RelativeItem(2).Border(0.75f).BorderColor(RuleColor).Padding(8).Column(customer =>
                {
                    customer.Item().Text("Facturé à").FontSize(8).SemiBold().FontColor(MutedText);
                    customer.Item().Text(model.Customer.Name).SemiBold();

                    foreach (var line in AddressLines(model.Customer))
                    {
                        customer.Item().Text(line);
                    }

                    foreach (var line in IdentifierLines(model.Customer))
                    {
                        customer.Item().Text(line).FontSize(8).FontColor(MutedText);
                    }
                });
            });

            column.Item().PaddingBottom(6);
        });
    }

    private static void ComposeContent(IContainer container, InvoiceDocumentModel model)
    {
        container.PaddingVertical(4).Column(column =>
        {
            column.Spacing(12);

            column.Item().Element(lines => ComposeLinesTable(lines, model));

            column.Item().Row(row =>
            {
                row.RelativeItem(3).Element(summary => ComposeVatSummary(summary, model));
                row.ConstantItem(20);
                row.RelativeItem(2).Element(totals => ComposeTotals(totals, model));
            });

            column.Item().PaddingTop(6).Text(model.PaymentTerms).FontSize(8);
        });
    }

    private static void ComposeLinesTable(IContainer container, InvoiceDocumentModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(5);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.9f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(2);
            });

            // L'en-tete se repete sur chaque page : une facture longue reste lisible page a page.
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("N°");
                header.Cell().Element(HeaderCell).Text("Désignation");
                header.Cell().Element(HeaderCell).AlignRight().Text("Quantité");
                header.Cell().Element(HeaderCell).AlignRight().Text("PU HT");
                header.Cell().Element(HeaderCell).AlignRight().Text("TVA");
                header.Cell().Element(HeaderCell).AlignRight().Text("Montant HT");
            });

            foreach (var line in model.Lines)
            {
                table.Cell().Element(BodyCell).Text(line.LineNumber.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(line.Designation);
                table.Cell().Element(BodyCell).AlignRight().Text(FormatQuantity(line.Quantity));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatAmount(line.UnitPrice));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatRate(line.VatRate));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatAmount(line.LineTotalExclVat));
            }
        });
    }

    private static void ComposeVatSummary(IContainer container, InvoiceDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().Text("Récapitulatif TVA").FontSize(8).SemiBold().FontColor(MutedText);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Taux");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Base HT");
                    header.Cell().Element(HeaderCell).AlignRight().Text("TVA");
                });

                foreach (var line in model.VatSummary)
                {
                    table.Cell().Element(BodyCell).Text(FormatRate(line.VatRate));
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatAmount(line.BaseExclVat));
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatAmount(line.VatAmount));
                }
            });
        });
    }

    private static void ComposeTotals(IContainer container, InvoiceDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Total HT");
                row.ConstantItem(95).AlignRight().Text(FormatAmount(model.TotalExclVat));
            });

            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text("Total TVA");
                row.ConstantItem(95).AlignRight().Text(FormatAmount(model.TotalVat));
            });

            column.Item().PaddingTop(4).BorderTop(1).BorderColor(RuleColor).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("Total TTC").FontSize(11).Bold();
                row.ConstantItem(95).AlignRight().Text(FormatAmount(model.TotalInclVat)).FontSize(11).Bold();
            });

            column.Item().PaddingTop(2).AlignRight().Text($"Montants en {model.CurrencyLabel}").FontSize(7).Italic().FontColor(MutedText);
        });
    }

    private static void ComposeFooter(IContainer container, InvoiceDocumentModel model)
    {
        container.Column(column =>
        {
            column.Item().BorderTop(0.5f).BorderColor(LightRuleColor).PaddingTop(4)
                .Text(model.FooterMentions).FontSize(7).FontColor(MutedText);

            column.Item().PaddingTop(2).AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(7).FontColor(MutedText));
                text.Span($"Facture {model.Number} - page ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(HeaderBackground)
            .BorderBottom(1)
            .BorderColor(RuleColor)
            .PaddingVertical(4)
            .PaddingHorizontal(4)
            .DefaultTextStyle(style => style.SemiBold());
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container
            .BorderBottom(0.5f)
            .BorderColor(LightRuleColor)
            .PaddingVertical(3)
            .PaddingHorizontal(4);
    }

    private static IEnumerable<string> AddressLines(DocumentPartyModel party)
    {
        if (!string.IsNullOrWhiteSpace(party.Address))
        {
            yield return party.Address;
        }

        if (!string.IsNullOrWhiteSpace(party.City))
        {
            yield return party.City;
        }
    }

    private static IEnumerable<string> IdentifierLines(DocumentPartyModel party)
    {
        if (!string.IsNullOrWhiteSpace(party.Nif))
        {
            yield return $"NIF : {party.Nif}";
        }

        if (!string.IsNullOrWhiteSpace(party.Rc))
        {
            yield return $"RC : {party.Rc}";
        }

        if (!string.IsNullOrWhiteSpace(party.Ai))
        {
            yield return $"AI : {party.Ai}";
        }

        if (!string.IsNullOrWhiteSpace(party.Nis))
        {
            yield return $"NIS : {party.Nis}";
        }
    }

    private static IEnumerable<string> ContactLines(DocumentPartyModel party)
    {
        if (!string.IsNullOrWhiteSpace(party.Phone))
        {
            yield return $"Tél. : {party.Phone}";
        }

        if (!string.IsNullOrWhiteSpace(party.Email))
        {
            yield return party.Email;
        }
    }

    private static string FormatAmount(decimal value)
    {
        return value.ToString("N2", FrenchAmounts);
    }

    private static string FormatQuantity(decimal value)
    {
        // Jusqu'a trois decimales (echelle de la colonne), sans zeros inutiles : « 2 », « 1,5 ».
        return value.ToString("#,##0.###", FrenchAmounts);
    }

    private static string FormatRate(decimal value)
    {
        return value.ToString("0.##", FrenchAmounts) + " %";
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }
}
