using System.Globalization;
using AldaJoyeros.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AldaJoyeros.Services.Implementations;

internal sealed class FacturaDocument(FacturaDto factura) : IDocument
{
    private const string Green = "#102d23";
    private const string Gold = "#b69b65";
    private const string Ink = "#192e27";
    private const string Muted = "#626d66";
    private const string Paper = "#f7f7f2";
    private const string Line = "#e1e5dd";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private static string Money(double amount) => amount.ToString("C2", Spanish);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Factura {factura.NumeroFactura}", Author = factura.Empresa.Nombre,
        Subject = $"Factura del pedido #{factura.Pedido.Id}", Creator = "Alda Joyeros - Facturación"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));
            page.Header().PaddingBottom(18).Element(Header);
            page.Content().Column(column =>
            {
                column.Spacing(18);
                column.Item().Element(InvoiceDetails);
                column.Item().Element(Parties);
                column.Item().Element(Products);
                column.Item().ShowEntire().Element(Totals);
                column.Item().ShowEntire().Background(Paper).BorderLeft(2).BorderColor(Gold).Padding(14).Column(note =>
                {
                    note.Item().Text("Gracias por confiar en AldaJoyeros.").SemiBold().FontSize(11);
                    note.Item().PaddingTop(5).Text("Conserva esta factura como justificante de tu compra. Para cualquier consulta, indica el número de factura o de pedido.").FontSize(9).FontColor(Muted);
                });
            });
            page.Footer().PaddingTop(16).Element(Footer);
        });
    }

    private void Header(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Background(Green).Padding(22).Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("ALDA").FontFamily("Georgia").FontSize(30).FontColor(Colors.White);
                    brand.Item().PaddingTop(3).Text("JOYEROS · DESDE 1962").FontSize(9).FontColor("#d9c69f");
                });
                row.ConstantItem(125).AlignRight().AlignMiddle().Column(title =>
                {
                    title.Item().AlignRight().Text("FACTURA").FontSize(21).SemiBold().FontColor(Colors.White);
                    title.Item().PaddingTop(5).AlignRight().Text($"Pedido #{factura.Pedido.Id}").FontSize(9).FontColor("#d9c69f");
                });
            });
            column.Item().Height(3).Background(Gold);
        });
    }

    private void InvoiceDetails(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem(2).Column(column =>
            {
                Label(column, "NÚMERO DE FACTURA");
                column.Item().PaddingTop(5).Text(factura.NumeroFactura).FontSize(15).SemiBold();
            });
            row.RelativeItem().Column(column =>
            {
                Label(column, "FECHA DE EMISIÓN");
                column.Item().PaddingTop(5).Text(factura.FechaEmision.ToString("dd/MM/yyyy", Spanish)).SemiBold();
            });
            row.RelativeItem().Column(column =>
            {
                Label(column, "FECHA DEL PEDIDO");
                column.Item().PaddingTop(5).Text(factura.Pedido.Fecha?.ToString("dd/MM/yyyy", Spanish) ?? "No disponible").SemiBold();
            });
        });
    }

    private void Parties(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().BorderTop(1).BorderColor(Line).PaddingTop(14).Column(column =>
            {
                column.Spacing(4);
                Label(column, "EMISOR");
                column.Item().PaddingTop(4).Text(factura.Empresa.Nombre).SemiBold().FontSize(11);
                if (!string.IsNullOrWhiteSpace(factura.Empresa.Cif)) column.Item().Text($"CIF: {factura.Empresa.Cif}").FontColor(Muted);
                column.Item().Text(factura.Empresa.Direccion).FontColor(Muted);
                column.Item().Text($"{factura.Empresa.CodigoPostal} {factura.Empresa.Ciudad}").FontColor(Muted);
                column.Item().Text(factura.Empresa.Provincia).FontColor(Muted);
            });
            row.ConstantItem(24);
            row.RelativeItem().Background(Paper).Padding(14).Column(column =>
            {
                column.Spacing(4);
                Label(column, "CLIENTE");
                // Los pedidos actuales pueden utilizar el email como nombre del cliente.
                var sameName = string.Equals(factura.Cliente.Nombre, factura.Cliente.Email, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(factura.Cliente.Nombre) && !sameName)
                    column.Item().PaddingTop(4).Text(factura.Cliente.Nombre).SemiBold().FontSize(11);
                column.Item().Text(factura.Cliente.Email).FontColor(Ink);
                if (!string.IsNullOrWhiteSpace(factura.Cliente.Nif)) column.Item().Text($"NIF: {factura.Cliente.Nif}").FontColor(Muted);
                column.Item().PaddingTop(4).Text(factura.Cliente.Direccion).FontColor(Muted);
                column.Item().Text($"{factura.Cliente.CodigoPostal} {factura.Cliente.Ciudad}").FontColor(Muted);
                column.Item().Text(factura.Cliente.Provincia).FontColor(Muted);
            });
        });
    }

    private static void Label(ColumnDescriptor column, string text) => column.Item().Text(text).FontSize(8).SemiBold().FontColor(Muted);

    private void Products(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(); columns.ConstantColumn(38); columns.ConstantColumn(86); columns.ConstantColumn(90);
            });
            table.Header(header =>
            {
                header.Cell().Element(Heading).Text("ARTÍCULO");
                header.Cell().Element(Heading).AlignCenter().Text("UDS.");
                header.Cell().Element(Heading).AlignRight().Text("PRECIO / UD.");
                header.Cell().Element(Heading).AlignRight().Text("IMPORTE");
            });
            var index = 0;
            foreach (var item in factura.Pedido.LineasPedido)
            {
                var background = index++ % 2 == 0 ? "#ffffff" : Paper;
                table.Cell().Element(c => Cell(c, background)).Text(string.IsNullOrWhiteSpace(item.ProductoNombre) ? "Artículo" : item.ProductoNombre);
                table.Cell().Element(c => Cell(c, background)).AlignCenter().Text(item.Cantidad.ToString(Spanish));
                table.Cell().Element(c => Cell(c, background)).AlignRight().Text(Money(item.Precio));
                table.Cell().Element(c => Cell(c, background)).AlignRight().Text(Money(item.Subtotal)).SemiBold();
            }
            if (index == 0)
                table.Cell().ColumnSpan(4).Element(c => Cell(c, Paper)).Text("No hay líneas de artículos disponibles.").FontColor(Muted);
        });
    }

    private static IContainer Heading(IContainer container) => container.Background(Green).PaddingVertical(11).PaddingHorizontal(9)
        .DefaultTextStyle(x => x.FontSize(8).SemiBold().FontColor(Colors.White));
    private static IContainer Cell(IContainer container, string background) => container.Background(background).BorderBottom(0.5f).BorderColor(Line)
        .PaddingVertical(10).PaddingHorizontal(9).DefaultTextStyle(x => x.FontSize(10));

    private void Totals(IContainer container)
    {
        container.AlignRight().Width(260).Column(column =>
        {
            column.Item().PaddingHorizontal(14).Row(row =>
            {
                row.RelativeItem().Text("Base imponible").FontColor(Muted);
                row.ConstantItem(110).AlignRight().Text(Money(factura.BaseImponible));
            });
            column.Item().PaddingTop(9).PaddingHorizontal(14).Row(row =>
            {
                row.RelativeItem().Text("IVA (21 %)").FontColor(Muted);
                row.ConstantItem(110).AlignRight().Text(Money(factura.ImporteIva));
            });
            column.Item().PaddingTop(14).Background(Green).Padding(14).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text("TOTAL").FontSize(11).SemiBold().FontColor(Colors.White);
                row.ConstantItem(145).AlignRight().Text(Money(factura.Total)).FontSize(20).SemiBold().FontColor(Colors.White);
            });
            column.Item().PaddingTop(6).AlignRight().Text("Precios con IVA incluido").FontSize(8).FontColor(Muted);
        });
    }

    private void Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Line).PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(factura.Empresa.Nombre).FontSize(8).SemiBold();
                column.Item().PaddingTop(3).Text($"{factura.Empresa.Email} | {factura.Empresa.Telefono}").FontSize(8).FontColor(Muted);
            });
            row.ConstantItem(85).AlignRight().AlignBottom().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                text.Span("Página "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
            });
        });
    }
}
