using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;

namespace AldaJoyeros.Controllers
{
    public class CarritoController : BaseController
    {
        private readonly ICarritoService _carritoService;
        private readonly IProductoService _productoService;
        private readonly IProductoImagenService _imagenService;

        public CarritoController(
            ICarritoService carritoService, 
            IProductoService productoService,
            IProductoImagenService imagenService)
        {
            _carritoService = carritoService;
            _productoService = productoService;
            _imagenService = imagenService;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            var items = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
            
            // Cargar imágenes para cada producto del carrito
            foreach (var item in items)
            {
                if (item.Producto != null)
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                    item.Producto.Imagenes = imagenes.ToList();
                }
            }
            
            var total = await _carritoService.GetTotalAsync(CurrentUser.Id);
            
            ViewBag.Total = total;
            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> Agregar(long productoId, int cantidad = 1)
        {
            if (!IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var carritoItemDto = new CarritoItemCreateDto
                {
                    ProductoId = productoId,
                    Cantidad = cantidad
                };

                await _carritoService.AddItemAsync(CurrentUser!.Id, carritoItemDto);
                TempData["Success"] = "Producto agregado al carrito";
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Detalle", "Productos", new { id = productoId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Actualizar(long itemId, int cantidad)
        {
            if (!IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var updateDto = new CarritoItemUpdateDto { Cantidad = cantidad };
                await _carritoService.UpdateItemAsync(CurrentUser!.Id, itemId, updateDto);
                TempData["Success"] = "Carrito actualizado";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long itemId)
        {
            if (!IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                await _carritoService.DeleteItemAsync(CurrentUser!.Id, itemId);
                TempData["Success"] = "Producto eliminado del carrito";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Vaciar()
        {
            if (!IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                await _carritoService.ClearCarritoAsync(CurrentUser!.Id);
                TempData["Success"] = "Carrito vaciado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
