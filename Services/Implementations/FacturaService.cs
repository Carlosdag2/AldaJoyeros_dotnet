using AldaJoyeros.Configuration;
using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Interfaces;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AldaJoyeros.Services.Implementations
{
    /// <summary>
    /// Servicio para generar facturas PDF usando QuestPDF
    /// </summary>
    public sealed class FacturaService : IFacturaService
    {
        private readonly EmpresaSettings _empresaSettings;
        private readonly ILogger<FacturaService> _logger;

        public FacturaService(
            IOptions<EmpresaSettings> empresaSettings,
            ILogger<FacturaService> logger)
        {
            _empresaSettings = empresaSettings.Value;
            _logger = logger;

            // Configurar licencia de QuestPDF (Community es gratuita)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerarFacturaPdfAsync(PedidoDto pedido, string emailCliente, string nombreCliente)
        {
            var factura = ObtenerDatosFactura(pedido, emailCliente, nombreCliente);
            
            return await Task.Run(() =>
            {
                var document = new FacturaDocument(factura);
                return document.GeneratePdf();
            });
        }

        public string GenerarNumeroFactura(long pedidoId)
        {
            var ano = DateTime.Now.Year;
            return $"{_empresaSettings.PrefijoFactura}-{ano}-{pedidoId:D6}";
        }

        public FacturaDto ObtenerDatosFactura(PedidoDto pedido, string emailCliente, string nombreCliente)
        {
            var direccion = pedido.Direccion;
            
            return new FacturaDto
            {
                NumeroFactura = GenerarNumeroFactura(pedido.Id),
                FechaEmision = DateTime.Now,
                Pedido = pedido,
                Empresa = new DatosEmpresaDto
                {
                    Nombre = _empresaSettings.Nombre,
                    Cif = _empresaSettings.Cif,
                    Direccion = _empresaSettings.Direccion,
                    CodigoPostal = _empresaSettings.CodigoPostal,
                    Ciudad = _empresaSettings.Ciudad,
                    Provincia = _empresaSettings.Provincia,
                    Telefono = _empresaSettings.Telefono,
                    Email = _empresaSettings.Email
                },
                Cliente = new DatosClienteDto
                {
                    Nombre = nombreCliente,
                    Email = emailCliente,
                    Direccion = direccion != null 
                        ? $"{direccion.Calle}, {direccion.Numero}{(!string.IsNullOrEmpty(direccion.Piso) ? $" - {direccion.Piso}" : "")}"
                        : "No especificada",
                    CodigoPostal = direccion?.CodigoPostal ?? "",
                    Ciudad = direccion?.Ciudad ?? "",
                    Provincia = direccion?.Provincia ?? ""
                }
            };
        }
    }

    /// <summary>
    /// Documento PDF de factura usando QuestPDF
    /// </summary>
    internal sealed class FacturaDocument : IDocument
    {
        private readonly FacturaDto _factura;

        // Colores corporativos
        private static readonly string ColorPrimario = "#0f172a";
        private static readonly string ColorSecundario = "#059669";
        private static readonly string ColorTexto = "#334155";
        private static readonly string ColorTextoClaro = "#64748b";
        private static readonly string ColorFondo = "#f8fafc";
        private static readonly string ColorBorde = "#e2e8f0";

        public FacturaDocument(FacturaDto factura)
        {
            _factura = factura;
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"Factura {_factura.NumeroFactura}",
            Author = _factura.Empresa.Nombre,
            Subject = $"Factura del pedido #{_factura.Pedido.Id}",
            Creator = "Alda Joyeros - Sistema de Facturación"
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginVertical(40);
                page.MarginHorizontal(50);

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                // Logo y título
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(_factura.Empresa.Nombre)
                            .FontSize(24)
                            .Bold()
                            .FontColor(ColorPrimario);
                        
                        col.Item().Text("Joyería desde 1962")
                            .FontSize(10)
                            .FontColor(ColorTextoClaro);
                    });

                    row.ConstantItem(150).AlignRight().Column(col =>
                    {
                        col.Item().Text("FACTURA")
                            .FontSize(28)
                            .Bold()
                            .FontColor(ColorSecundario);
                    });
                });

                column.Item().PaddingTop(20).LineHorizontal(1).LineColor(ColorBorde);
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Información de factura y cliente
                column.Item().Row(row =>
                {
                    // Datos de factura
                    row.RelativeItem().Component(new DatosFacturaComponent(_factura));
                    
                    row.ConstantItem(30);
                    
                    // Datos del cliente
                    row.RelativeItem().Component(new DatosClienteComponent(_factura.Cliente));
                });

                column.Item().PaddingVertical(20);

                // Tabla de productos
                column.Item().Element(ComposeTablaProductos);

                column.Item().PaddingVertical(15);

                // Totales
                column.Item().Element(ComposeTotales);

                column.Item().PaddingVertical(20);

                // Información adicional
                column.Item().Element(ComposeInfoAdicional);
            });
        }

        private void ComposeTablaProductos(IContainer container)
        {
            container.Table(table =>
            {
                // Definir columnas
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Descripción
                    columns.ConstantColumn(60); // Cantidad
                    columns.ConstantColumn(80); // Precio
                    columns.ConstantColumn(80); // Subtotal
                });

                // Cabecera
                table.Header(header =>
                {
                    header.Cell().Element(CeldaCabecera).Text("Descripción");
                    header.Cell().Element(CeldaCabecera).AlignCenter().Text("Cant.");
                    header.Cell().Element(CeldaCabecera).AlignRight().Text("Precio");
                    header.Cell().Element(CeldaCabecera).AlignRight().Text("Subtotal");

                    static IContainer CeldaCabecera(IContainer container)
                    {
                        return container
                            .Background("#0f172a")
                            .Padding(8)
                            .DefaultTextStyle(x => x.FontColor(Colors.White).Bold().FontSize(10));
                    }
                });

                // Filas de productos
                var esImpar = true;
                foreach (var linea in _factura.Pedido.LineasPedido)
                {
                    var bgColor = esImpar ? "#ffffff" : "#f8fafc";
                    
                    table.Cell().Element(c => CeldaContenido(c, bgColor))
                        .Text(linea.ProductoNombre ?? "Producto");
                    
                    table.Cell().Element(c => CeldaContenido(c, bgColor))
                        .AlignCenter()
                        .Text(linea.Cantidad.ToString());
                    
                    table.Cell().Element(c => CeldaContenido(c, bgColor))
                        .AlignRight()
                        .Text(linea.Precio.ToString("C"));
                    
                    table.Cell().Element(c => CeldaContenido(c, bgColor))
                        .AlignRight()
                        .Text(linea.Subtotal.ToString("C"))
                        .Bold();

                    esImpar = !esImpar;
                }

                static IContainer CeldaContenido(IContainer container, string bgColor)
                {
                    return container
                        .Background(bgColor)
                        .BorderBottom(1)
                        .BorderColor("#e2e8f0")
                        .Padding(8)
                        .DefaultTextStyle(x => x.FontSize(10).FontColor("#334155"));
                }
            });
        }

        private void ComposeTotales(IContainer container)
        {
            container.AlignRight().Width(250).Column(column =>
            {
                // Base imponible
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Base Imponible:")
                        .FontSize(10)
                        .FontColor(ColorTextoClaro);
                    row.ConstantItem(100).AlignRight().Text(_factura.BaseImponible.ToString("C"))
                        .FontSize(10)
                        .FontColor(ColorTexto);
                });

                column.Item().PaddingVertical(4);

                // IVA
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("IVA (21%):")
                        .FontSize(10)
                        .FontColor(ColorTextoClaro);
                    row.ConstantItem(100).AlignRight().Text(_factura.ImporteIva.ToString("C"))
                        .FontSize(10)
                        .FontColor(ColorTexto);
                });

                column.Item().PaddingVertical(8);
                column.Item().LineHorizontal(1).LineColor(ColorBorde);
                column.Item().PaddingVertical(8);

                // Total
                column.Item().Background(ColorSecundario).Padding(12).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL:")
                        .FontSize(14)
                        .Bold()
                        .FontColor(Colors.White);
                    row.ConstantItem(100).AlignRight().Text(_factura.Total.ToString("C"))
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.White);
                });
            });
        }

        private void ComposeInfoAdicional(IContainer container)
        {
            container.Background(ColorFondo).Padding(15).Column(column =>
            {
                column.Item().Text("Información de pago")
                    .FontSize(11)
                    .Bold()
                    .FontColor(ColorPrimario);
                
                column.Item().PaddingTop(8).Text("Pago realizado mediante tarjeta de crédito/débito a través de Stripe.")
                    .FontSize(9)
                    .FontColor(ColorTextoClaro);

                column.Item().PaddingTop(4).Text("Esta factura sirve como justificante de compra y garantía.")
                    .FontSize(9)
                    .FontColor(ColorTextoClaro);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(ColorBorde);
                
                column.Item().PaddingTop(15).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(_factura.Empresa.Nombre)
                            .FontSize(9)
                            .Bold()
                            .FontColor(ColorPrimario);
                        
                        if (!string.IsNullOrEmpty(_factura.Empresa.Cif))
                        {
                            col.Item().Text($"CIF: {_factura.Empresa.Cif}")
                                .FontSize(8)
                                .FontColor(ColorTextoClaro);
                        }
                        
                        col.Item().Text($"{_factura.Empresa.Direccion}")
                            .FontSize(8)
                            .FontColor(ColorTextoClaro);
                        
                        col.Item().Text($"{_factura.Empresa.CodigoPostal} {_factura.Empresa.Ciudad}, {_factura.Empresa.Provincia}")
                            .FontSize(8)
                            .FontColor(ColorTextoClaro);
                    });

                    row.RelativeItem().AlignRight().Column(col =>
                    {
                        col.Item().Text(_factura.Empresa.Telefono)
                            .FontSize(8)
                            .FontColor(ColorTextoClaro);
                        
                        col.Item().Text(_factura.Empresa.Email)
                            .FontSize(8)
                            .FontColor(ColorTextoClaro);
                        
                        col.Item().PaddingTop(8)
                            .Text(text =>
                            {
                                text.DefaultTextStyle(x => x.FontSize(8).FontColor(ColorTextoClaro));
                                text.Span("Página ");
                                text.CurrentPageNumber();
                                text.Span(" de ");
                                text.TotalPages();
                            });
                    });
                });
            });
        }
    }

    /// <summary>
    /// Componente para los datos de la factura
    /// </summary>
    internal sealed class DatosFacturaComponent : IComponent
    {
        private readonly FacturaDto _factura;

        public DatosFacturaComponent(FacturaDto factura)
        {
            _factura = factura;
        }

        public void Compose(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Datos de Factura")
                    .FontSize(11)
                    .Bold()
                    .FontColor("#0f172a");

                column.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                    });

                    FilaDato(table, "Nº Factura:", _factura.NumeroFactura);
                    FilaDato(table, "Fecha:", _factura.FechaEmision.ToString("dd/MM/yyyy"));
                    FilaDato(table, "Nº Pedido:", $"#{_factura.Pedido.Id}");
                    FilaDato(table, "Fecha Pedido:", _factura.Pedido.Fecha?.ToString("dd/MM/yyyy") ?? "-");
                });
            });
        }

        private static void FilaDato(TableDescriptor table, string etiqueta, string valor)
        {
            table.Cell().PaddingVertical(2).Text(etiqueta)
                .FontSize(9)
                .FontColor("#64748b");
            
            table.Cell().PaddingVertical(2).Text(valor)
                .FontSize(9)
                .Bold()
                .FontColor("#334155");
        }
    }

    /// <summary>
    /// Componente para los datos del cliente
    /// </summary>
    internal sealed class DatosClienteComponent : IComponent
    {
        private readonly DatosClienteDto _cliente;

        public DatosClienteComponent(DatosClienteDto cliente)
        {
            _cliente = cliente;
        }

        public void Compose(IContainer container)
        {
            container.Background("#f8fafc").Padding(12).Column(column =>
            {
                column.Item().Text("Facturar a:")
                    .FontSize(11)
                    .Bold()
                    .FontColor("#0f172a");

                column.Item().PaddingTop(8).Text(_cliente.Nombre)
                    .FontSize(10)
                    .Bold()
                    .FontColor("#334155");

                column.Item().PaddingTop(2).Text(_cliente.Email)
                    .FontSize(9)
                    .FontColor("#64748b");

                if (!string.IsNullOrEmpty(_cliente.Nif))
                {
                    column.Item().PaddingTop(2).Text($"NIF: {_cliente.Nif}")
                        .FontSize(9)
                        .FontColor("#64748b");
                }

                column.Item().PaddingTop(8).Text(_cliente.Direccion)
                    .FontSize(9)
                    .FontColor("#334155");

                column.Item().Text($"{_cliente.CodigoPostal} {_cliente.Ciudad}")
                    .FontSize(9)
                    .FontColor("#334155");

                column.Item().Text(_cliente.Provincia)
                    .FontSize(9)
                    .FontColor("#334155");
            });
        }
    }
}
