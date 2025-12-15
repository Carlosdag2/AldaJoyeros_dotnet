using AldaJoyeros.Attributes;
using AldaJoyeros.Extensions;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers
{
    /// <summary>
    /// Controlador para gestionar facturas
    /// </summary>
    [JwtAuthorize]
    public class FacturasController : BaseController
    {
        private readonly IFacturaService _facturaService;
        private readonly IPedidoService _pedidoService;
        private readonly IUsuarioService _usuarioService;
        private readonly IEmailService _emailService;
        private readonly ILogger<FacturasController> _logger;

        public FacturasController(
            IFacturaService facturaService,
            IPedidoService pedidoService,
            IUsuarioService usuarioService,
            IEmailService emailService,
            ILogger<FacturasController> logger)
        {
            _facturaService = facturaService;
            _pedidoService = pedidoService;
            _usuarioService = usuarioService;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Descarga la factura en PDF de un pedido
        /// </summary>
        [HttpGet("Pedidos/{id}/Factura")]
        public async Task<IActionResult> Descargar(long id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
            {
                TempData["Error"] = "Pedido no encontrado";
                return RedirectToAction("Index", "Pedidos");
            }

            // Verificar que el pedido pertenece al usuario (o es admin)
            if (pedido.UsuarioId != userId && !User.IsAdmin())
            {
                TempData["Error"] = "No tienes permiso para ver esta factura";
                return RedirectToAction("Index", "Pedidos");
            }

            var usuario = await _usuarioService.GetByIdAsync(userId.Value);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var pdfBytes = await _facturaService.GenerarFacturaPdfAsync(
                    pedido, 
                    usuario.Email, 
                    usuario.Email); // Usamos email como nombre si no hay nombre

                var numeroFactura = _facturaService.GenerarNumeroFactura(pedido.Id);
                var fileName = $"Factura_{numeroFactura.Replace("/", "-")}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar factura para pedido {PedidoId}", id);
                TempData["Error"] = "Error al generar la factura";
                return RedirectToAction("Detalle", "Pedidos", new { id });
            }
        }

        /// <summary>
        /// Envía la factura por email al usuario
        /// </summary>
        [HttpPost("Pedidos/{id}/EnviarFactura")]
        public async Task<IActionResult> Enviar(long id)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "No autenticado" });
            }

            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
            {
                return Json(new { success = false, message = "Pedido no encontrado" });
            }

            // Verificar que el pedido pertenece al usuario (o es admin)
            if (pedido.UsuarioId != userId && !User.IsAdmin())
            {
                return Json(new { success = false, message = "No tienes permiso" });
            }

            var usuario = await _usuarioService.GetByIdAsync(userId.Value);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Usuario no encontrado" });
            }

            try
            {
                var pdfBytes = await _facturaService.GenerarFacturaPdfAsync(
                    pedido, 
                    usuario.Email, 
                    usuario.Email);

                var numeroFactura = _facturaService.GenerarNumeroFactura(pedido.Id);

                await _emailService.SendInvoiceEmailAsync(
                    usuario.Email,
                    pedido,
                    pdfBytes,
                    numeroFactura);

                _logger.LogInformation("Factura {NumeroFactura} enviada a {Email}", numeroFactura, usuario.Email);

                return Json(new { success = true, message = "Factura enviada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar factura para pedido {PedidoId}", id);
                return Json(new { success = false, message = "Error al enviar la factura" });
            }
        }
    }
}
