using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stockly.Application.Common;
using Stockly.Application.Features.Invoices;
using Stockly.Domain.Enums;

namespace Stockly.Infrastructure.Pdf;

public sealed class CompanyOptions
{
    public const string Section = "Company";

    public string Name { get; set; } = "Stockly Distribuidora S.R.L.";
    public string TaxId { get; set; } = "30-71234567-8";
    public string TaxCondition { get; set; } = "IVA Responsable Inscripto";
    public string Address { get; set; } = "Av. Corrientes 1234, CABA";
    public string? Email { get; set; } = "ventas@stockly.example";
    public string? Phone { get; set; }
    public string GrossIncome { get; set; } = "901-123456-7";
    public DateOnly ActivityStart { get; set; } = new(2021, 3, 1);
}

/// <summary>
/// Builds the invoice PDF with QuestPDF's fluent layout API (no HTML-to-PDF, no headless browser).
/// The layout mirrors an Argentine invoice: letter box, emitter/receiver data, VAT discriminated by rate.
/// </summary>
internal sealed class QuestPdfInvoiceRenderer(IOptions<CompanyOptions> company) : IInvoicePdfRenderer
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");
    private const string Primary = "#4F46E5";
    private const string Muted = "#6B7280";
    private const string Border = "#E5E7EB";

    static QuestPdfInvoiceRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(InvoiceDetailDto invoice)
    {
        var c = company.Value;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor("#111827"));

            if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
            {
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text(invoice.Status == InvoiceStatus.Draft ? "BORRADOR" : "ANULADA")
                    .FontSize(90).Bold().FontColor("#EF444433");
            }

            page.Header().Element(h => Header(h, invoice, c));
            page.Content().PaddingVertical(14).Column(col =>
            {
                col.Spacing(14);
                col.Item().Element(e => Customer(e, invoice));
                col.Item().Element(e => Lines(e, invoice));
                col.Item().Element(e => Totals(e, invoice));
                if (!string.IsNullOrWhiteSpace(invoice.Notes))
                {
                    col.Item().Background("#F9FAFB").Padding(8).Text(t =>
                    {
                        t.Span("Observaciones: ").SemiBold();
                        t.Span(invoice.Notes);
                    });
                }

                if (invoice.Status == InvoiceStatus.Cancelled)
                {
                    col.Item().Text($"Anulada: {invoice.CancellationReason}").FontColor("#B91C1C");
                }
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text("Comprobante generado por Stockly · documento demostrativo sin validez fiscal (sin CAE).")
                    .FontSize(7.5f).FontColor(Muted);
                row.ConstantItem(80).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Header(IContainer container, InvoiceDetailDto invoice, CompanyOptions c)
    {
        container.Border(1).BorderColor(Border).Row(row =>
        {
            row.RelativeItem().Padding(12).Column(col =>
            {
                col.Item().Text(c.Name).FontSize(16).Bold().FontColor(Primary);
                col.Item().Text(c.Address).FontColor(Muted);
                col.Item().Text(c.TaxCondition).FontColor(Muted);
                if (c.Email is not null)
                {
                    col.Item().Text(c.Email).FontColor(Muted);
                }
            });

            row.ConstantItem(70).BorderLeft(1).BorderRight(1).BorderColor(Border).AlignCenter().PaddingTop(10).Column(col =>
            {
                col.Item().AlignCenter().Text(invoice.Letter.ToString()).FontSize(34).Bold();
                col.Item().AlignCenter().Text(invoice.Letter == InvoiceLetter.A ? "COD. 01" : "COD. 06").FontSize(7).FontColor(Muted);
            });

            row.RelativeItem().Padding(12).Column(col =>
            {
                col.Item().Text("FACTURA").FontSize(16).Bold();
                col.Item().Text(t =>
                {
                    t.Span("N° ").SemiBold();
                    t.Span(invoice.Number ?? "— (borrador)");
                });
                col.Item().Text($"Fecha de emisión: {Date(invoice.IssueDate)}");
                col.Item().Text($"Vencimiento: {Date(invoice.DueDate)}");
                col.Item().PaddingTop(4).Text($"CUIT: {c.TaxId}").FontColor(Muted);
                col.Item().Text($"Ingresos Brutos: {c.GrossIncome}").FontColor(Muted);
                col.Item().Text($"Inicio de actividades: {Date(c.ActivityStart)}").FontColor(Muted);
            });
        });
    }

    private static void Customer(IContainer container, InvoiceDetailDto invoice)
    {
        var customer = invoice.Customer;
        container.Border(1).BorderColor(Border).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(t =>
                {
                    t.Span("Cliente: ").SemiBold();
                    t.Span(customer.Name);
                });
                col.Item().Text(t =>
                {
                    t.Span("Domicilio: ").SemiBold();
                    t.Span(customer.Address ?? "—");
                });
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(t =>
                {
                    t.Span(customer.TaxId.Length == 11 ? "CUIT: " : "DNI: ").SemiBold();
                    t.Span(FormatTaxId(customer.TaxId));
                });
                col.Item().Text(t =>
                {
                    t.Span("Condición IVA: ").SemiBold();
                    t.Span(TaxConditionLabel(customer.TaxCondition));
                });
            });
        });
    }

    private static void Lines(IContainer container, InvoiceDetailDto invoice)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(72);
                cols.RelativeColumn(3);
                cols.ConstantColumn(38);
                cols.ConstantColumn(80);
                cols.ConstantColumn(40);
                cols.ConstantColumn(38);
                cols.ConstantColumn(74);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "SKU", "Descripción", "Cant.", "P. unit.", "Bonif.", "IVA", "Subtotal" })
                {
                    h.Cell().Background(Primary).PaddingVertical(5).PaddingHorizontal(4)
                        .Text(title).FontColor(Colors.White).SemiBold().FontSize(8.5f);
                }
            });

            var even = false;
            foreach (var line in invoice.Lines)
            {
                string bg = even ? "#F9FAFB" : "#FFFFFF";
                even = !even;
                Cell(table, bg).Text(line.Sku).FontSize(8);
                Cell(table, bg).Text(line.Description);
                Cell(table, bg).AlignRight().Text(line.Quantity.ToString(Ar));
                Cell(table, bg).AlignRight().Text(Currency(line.UnitPrice));
                Cell(table, bg).AlignRight().Text(line.DiscountPercent == 0 ? "—" : $"{line.DiscountPercent.ToString("0.##", Ar)}%");
                Cell(table, bg).AlignRight().Text(line.VatRate.ToLabel());
                Cell(table, bg).AlignRight().Text(Currency(line.NetAmount));
            }
        });
    }

    private static IContainer Cell(TableDescriptor table, string background) =>
        table.Cell().Background(background).BorderBottom(1).BorderColor(Border).PaddingVertical(4).PaddingHorizontal(4);

    private static void Totals(IContainer container, InvoiceDetailDto invoice)
    {
        container.AlignRight().Width(250).Column(col =>
        {
            void Row(string label, decimal value, bool strong = false)
            {
                col.Item().PaddingVertical(2).Row(r =>
                {
                    var l = r.RelativeItem().Text(label);
                    var v = r.ConstantItem(110).AlignRight().Text(Currency(value));
                    if (strong)
                    {
                        l.Bold().FontSize(12);
                        v.Bold().FontSize(12);
                    }
                });
            }

            Row("Subtotal", invoice.Subtotal);
            if (invoice.DiscountTotal > 0)
            {
                Row("Bonificaciones", -invoice.DiscountTotal);
            }

            Row("Neto gravado", invoice.NetTotal);
            if (invoice.Vat21 > 0)
            {
                Row("IVA 21%", invoice.Vat21);
            }

            if (invoice.Vat105 > 0)
            {
                Row("IVA 10,5%", invoice.Vat105);
            }

            col.Item().PaddingTop(4).BorderTop(1).BorderColor(Border);
            Row("TOTAL", invoice.Total, strong: true);

            if (invoice.PaidAt is not null)
            {
                col.Item().PaddingTop(6).AlignRight().Text($"Pagada · {invoice.PaymentMethod}").FontColor("#047857").SemiBold();
            }
        });
    }

    private static string Currency(decimal value) => value.ToString("C2", Ar);

    private static string Date(DateOnly? date) => date?.ToString("dd/MM/yyyy", Ar) ?? "—";

    private static string FormatTaxId(string taxId) =>
        taxId.Length == 11 ? $"{taxId[..2]}-{taxId[2..10]}-{taxId[10]}" : taxId;

    private static string TaxConditionLabel(TaxCondition condition) => condition switch
    {
        TaxCondition.ResponsableInscripto => "Responsable Inscripto",
        TaxCondition.Monotributista => "Responsable Monotributo",
        TaxCondition.Exento => "IVA Exento",
        _ => "Consumidor Final",
    };
}
