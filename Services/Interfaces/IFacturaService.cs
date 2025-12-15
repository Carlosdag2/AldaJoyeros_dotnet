using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Interfaces
{
    /// <summary>
    /// Servicio para generar y enviar facturas
    /// </summary>
    public interface IFacturaService
    {
        /// <summary>
        /// Genera una factura PDF para un pedido
        /// </summary>
        /// <param name="pedido">Datos del pedido</param>
        /// <param name="emailCliente">Email del cliente</param>
        /// <param name="nombreCliente">Nombre del cliente</param>
        /// <returns>Bytes del PDF generado</returns>
        Task<byte[]> GenerarFacturaPdfAsync(PedidoDto pedido, string emailCliente, string nombreCliente);

        /// <summary>
        /// Genera el número de factura para un pedido
        /// </summary>
        /// <param name="pedidoId">ID del pedido</param>
        /// <returns>Número de factura formateado</returns>
        string GenerarNumeroFactura(long pedidoId);

        /// <summary>
        /// Obtiene los datos de factura para un pedido
        /// </summary>
        /// <param name="pedido">Datos del pedido</param>
        /// <param name="emailCliente">Email del cliente</param>
        /// <param name="nombreCliente">Nombre del cliente</param>
        /// <returns>Datos de la factura</returns>
        FacturaDto ObtenerDatosFactura(PedidoDto pedido, string emailCliente, string nombreCliente);
    }
}
