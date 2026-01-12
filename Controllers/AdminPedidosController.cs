using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;
using AldaJoyeros.Entities;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
    public class AdminPedidosController : BaseController
    {
        private readonly IPedidoService _pedidoService;
        private readonly IProductoImagenService _imagenService;
        private const int PageSize = 15;

        public AdminPedidosController(
            IPedidoService pedidoService,
            IProductoImagenService imagenService)
        {
            _pedidoService = pedidoService;
            _imagenService = imagenService;
        }

        public async Task<IActionResult> Index(string estado = "", int page = 1)
        {
            var pedidosQuery = await _pedidoService.GetAllAsync();
            
            var todosPedidos = pedidosQuery.ToList();
            var totalPendientes = todosPedidos.Count(p => p.Estado == "PENDIENTE");
            var totalEnProceso = todosPedidos.Count(p => p.Estado == "EN_PROCESO");
            var totalEnviados = todosPedidos.Count(p => p.Estado == "ENVIADO");
            var totalEntregados = todosPedidos.Count(p => p.Estado == "ENTREGADO");
            var totalCancelados = todosPedidos.Count(p => p.Estado == "CANCELADO");
            
            ViewBag.TotalPendientes = totalPendientes;
            ViewBag.TotalEnProceso = totalEnProceso;
            ViewBag.TotalEnviados = totalEnviados;
            ViewBag.TotalEntregados = totalEntregados;
            ViewBag.TotalCancelados = totalCancelados;
            
            if (!string.IsNullOrEmpty(estado))
            {
                pedidosQuery = pedidosQuery.Where(p => p.Estado == estado).ToList();
            }

            var pagedResult = PagedResult<PedidoDto>.Create(pedidosQuery, page, PageSize);
            ViewBag.EstadoFiltro = estado;

            // Si es petición AJAX, devolver partial con estadísticas en headers
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                Response.Headers.Append("X-Stats-Pendientes", totalPendientes.ToString());
                Response.Headers.Append("X-Stats-EnProceso", totalEnProceso.ToString());
                Response.Headers.Append("X-Stats-Enviados", totalEnviados.ToString());
                Response.Headers.Append("X-Stats-Entregados", totalEntregados.ToString());
                Response.Headers.Append("X-Stats-Total", pagedResult.TotalItems.ToString());
                return PartialView("_PedidosList", pagedResult);
            }

            return View(pagedResult);
        }

        public async Task<IActionResult> Detalle(long id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
            {
                TempData["Error"] = "Pedido no encontrado";
                return RedirectToAction("Index");
            }

            foreach (var linea in pedido.LineasPedido)
            {
                if (linea.Producto != null && linea.ProductoId.HasValue)
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(linea.ProductoId.Value);
                    linea.Producto.Imagenes = imagenes.ToList();
                }
            }

            return View(pedido);
        }

        [HttpGet]
        public async Task<IActionResult> EditarEstado(long id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
            {
                TempData["Error"] = "Pedido no encontrado";
                return RedirectToAction("Index");
            }

            var updateDto = new PedidoUpdateEstadoDto
            {
                Estado = Enum.Parse<EstadoPedido>(pedido.Estado ?? "PENDIENTE")
            };

            ViewBag.PedidoId = id;
            ViewBag.PedidoInfo = pedido;
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> EditarEstado(long id, PedidoUpdateEstadoDto pedidoDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, selecciona un estado válido";
                ViewBag.PedidoId = id;
                return View(pedidoDto);
            }

            try
            {
                var pedidoAnterior = await _pedidoService.GetByIdAsync(id);
                var estadoAnterior = pedidoAnterior?.Estado ?? "PENDIENTE";
                
                await _pedidoService.UpdateEstadoAsync(id, pedidoDto);
                
                var mensaje = pedidoDto.Estado switch
                {
                    EstadoPedido.EN_PROCESO => "El pedido está ahora en proceso de preparación",
                    EstadoPedido.ENVIADO => "El pedido ha sido marcado como enviado",
                    EstadoPedido.ENTREGADO => "El pedido ha sido marcado como entregado",
                    EstadoPedido.CANCELADO => "El pedido ha sido cancelado",
                    _ => "Estado del pedido actualizado"
                };
                
                TempData["Success"] = mensaje;
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar el estado: {ex.Message}";
                ViewBag.PedidoId = id;
                return View(pedidoDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                var pedido = await _pedidoService.GetByIdAsync(id);
                
                // Advertir si el pedido está en proceso o enviado
                if (pedido != null && (pedido.Estado == "EN_PROCESO" || pedido.Estado == "ENVIADO"))
                {
                    TempData["Warning"] = "Has eliminado un pedido que estaba en proceso o enviado. Asegúrate de notificar al cliente.";
                }
                
                await _pedidoService.DeleteAsync(id);
                TempData["Success"] = $"Pedido #{id} eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar el pedido: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        private static string GetEstadoDisplay(string estado)
        {
            return estado switch
            {
                "PENDIENTE" => "Pendiente",
                "EN_PROCESO" => "En Proceso",
                "ENVIADO" => "Enviado",
                "ENTREGADO" => "Entregado",
                "CANCELADO" => "Cancelado",
                _ => estado
            };
        }
    }
}
