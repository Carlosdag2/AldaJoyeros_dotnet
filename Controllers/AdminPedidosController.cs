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
            
            // Calcular estadísticas por estado
            var todosPedidos = pedidosQuery.ToList();
            ViewBag.TotalPendientes = todosPedidos.Count(p => p.Estado == "PENDIENTE");
            ViewBag.TotalEnProceso = todosPedidos.Count(p => p.Estado == "EN_PROCESO");
            ViewBag.TotalEnviados = todosPedidos.Count(p => p.Estado == "ENVIADO");
            ViewBag.TotalEntregados = todosPedidos.Count(p => p.Estado == "ENTREGADO");
            ViewBag.TotalCancelados = todosPedidos.Count(p => p.Estado == "CANCELADO");
            
            if (!string.IsNullOrEmpty(estado))
            {
                pedidosQuery = pedidosQuery.Where(p => p.Estado == estado).ToList();
            }

            var pagedResult = PagedResult<PedidoDto>.Create(pedidosQuery, page, PageSize);
            ViewBag.EstadoFiltro = estado;

            return View(pagedResult);
        }

        public async Task<IActionResult> Detalle(long id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null)
            {
                return NotFound();
            }

            // Cargar imágenes de MongoDB para cada producto en las líneas del pedido
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
                return NotFound();
            }

            var updateDto = new PedidoUpdateEstadoDto
            {
                Estado = Enum.Parse<EstadoPedido>(pedido.Estado)
            };

            ViewBag.PedidoId = id;
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> EditarEstado(long id, PedidoUpdateEstadoDto pedidoDto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.PedidoId = id;
                return View(pedidoDto);
            }

            try
            {
                await _pedidoService.UpdateEstadoAsync(id, pedidoDto);
                TempData["Success"] = "Estado del pedido actualizado exitosamente";
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.PedidoId = id;
                return View(pedidoDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                await _pedidoService.DeleteAsync(id);
                TempData["Success"] = "Pedido eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
