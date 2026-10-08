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

}
