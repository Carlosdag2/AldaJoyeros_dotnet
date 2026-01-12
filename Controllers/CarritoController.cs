using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;

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

        [JwtAuthorize]
        public async Task<IActionResult> Index()
        {
            var items = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
            
            foreach (var item in items)
            {
                if (item.Producto != null)
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                    item.Producto.Imagenes = imagenes.ToList();
                }
            }
            
            var total = await _carritoService.GetTotalAsync(CurrentUser.Id);
            
            if (!items.Any())
            {
                TempData["Info"] = "Tu carrito está vacío. ¡Explora nuestras colecciones!";
            }
            
            ViewBag.Total = total;
            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> Agregar(long productoId, int cantidad = 1)
        {
            // Validar cantidad
            if (cantidad < 1)
            {
                TempData["Error"] = "La cantidad debe ser al menos 1";
                return RedirectToAction("Detalle", "Productos", new { id = productoId });
            }

            if (!IsAuthenticated)
            {
                TempCarritoHelper.AddItem(HttpContext, productoId, cantidad);
                TempData["Info"] = "Producto guardado temporalmente. Inicia sesión para completar tu compra.";
                
                HttpContext.Session.SetString("ReturnUrl", Request.Headers["Referer"].ToString() ?? Url.Action("Index", "Productos")!);
                
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                var producto = await _productoService.GetByIdAsync(productoId);
                if (producto == null)
                {
                    TempData["Error"] = "El producto no existe";
                    return RedirectToAction("Index", "Productos");
                }

                var carritoItemDto = new CarritoItemCreateDto
                {
                    ProductoId = productoId,
                    Cantidad = cantidad
                };

                await _carritoService.AddItemAsync(CurrentUser!.Id, carritoItemDto);
                TempData["Success"] = $"'{producto.Nombre}' añadido al carrito";
                
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo añadir al carrito: {ex.Message}";
                return RedirectToAction("Detalle", "Productos", new { id = productoId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AgregarAjax(long productoId, int cantidad = 1)
        {
            if (cantidad < 1)
            {
                return Json(new { success = false, message = "La cantidad debe ser al menos 1" });
            }

            if (!IsAuthenticated)
            {
                // Guardar el producto temporalmente en la sesión
                TempCarritoHelper.AddItem(HttpContext, productoId, cantidad);
                
                // Guardar la URL actual para redirigir después del login
                var returnUrl = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    HttpContext.Session.SetString("ReturnUrl", returnUrl);
                }
                
                return Json(new { 
                    success = false, 
                    requiresLogin = true, 
                    message = "Producto guardado. Inicia sesión para completar tu compra.",
                    savedToTemp = true
                });
            }

            try
            {
                var producto = await _productoService.GetByIdAsync(productoId);
                if (producto == null)
                {
                    return Json(new { success = false, message = "El producto no existe" });
                }

                var carritoItemDto = new CarritoItemCreateDto
                {
                    ProductoId = productoId,
                    Cantidad = cantidad
                };

                await _carritoService.AddItemAsync(CurrentUser!.Id, carritoItemDto);
                
                var totalItems = await _carritoService.GetTotalItemsAsync(CurrentUser.Id);

                return Json(new { 
                    success = true, 
                    message = $"'{producto.Nombre}' añadido al carrito",
                    productoNombre = producto.Nombre,
                    totalItems = totalItems
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"No se pudo añadir al carrito: {ex.Message}" });
            }
        }

        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> Actualizar(long itemId, int cantidad)
        {
            try
            {
                if (cantidad < 1)
                {
                    TempData["Warning"] = "La cantidad mínima es 1. Si deseas eliminar el producto, usa el botón eliminar.";
                    return RedirectToAction("Index");
                }

                if (cantidad > 99)
                {
                    TempData["Warning"] = "La cantidad máxima por producto es 99";
                    return RedirectToAction("Index");
                }

                var updateDto = new CarritoItemUpdateDto { Cantidad = cantidad };
                await _carritoService.UpdateItemAsync(CurrentUser!.Id, itemId, updateDto);
                TempData["Success"] = "Cantidad actualizada";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> Eliminar(long itemId)
        {
            try
            {
                await _carritoService.DeleteItemAsync(CurrentUser!.Id, itemId);
                TempData["Success"] = "Producto eliminado del carrito";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> Vaciar()
        {
            try
            {
                var items = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                if (!items.Any())
                {
                    TempData["Info"] = "El carrito ya está vacío";
                    return RedirectToAction("Index");
                }

                await _carritoService.ClearCarritoAsync(CurrentUser!.Id);
                TempData["Success"] = "Carrito vaciado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al vaciar el carrito: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        #region API AJAX

        /// <summary>
        /// Actualizar cantidad de un item via AJAX
        /// </summary>
        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> ActualizarAjax(long itemId, int cantidad)
        {
            try
            {
                if (cantidad < 1)
                {
                    return Json(new { success = false, message = "La cantidad mínima es 1" });
                }

                if (cantidad > 99)
                {
                    return Json(new { success = false, message = "La cantidad máxima es 99" });
                }

                var updateDto = new CarritoItemUpdateDto { Cantidad = cantidad };
                await _carritoService.UpdateItemAsync(CurrentUser!.Id, itemId, updateDto);

                // Obtener datos actualizados
                var items = await _carritoService.GetByUsuarioIdAsync(CurrentUser.Id);
                var item = items.FirstOrDefault(i => i.Id == itemId);
                var total = await _carritoService.GetTotalAsync(CurrentUser.Id);
                var totalItems = await _carritoService.GetTotalItemsAsync(CurrentUser.Id);

                return Json(new
                {
                    success = true,
                    message = "Cantidad actualizada",
                    subtotal = item?.Subtotal ?? 0,
                    subtotalFormateado = (item?.Subtotal ?? 0).ToString("C"),
                    total = total,
                    totalFormateado = total.ToString("C"),
                    totalItems = totalItems
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar item del carrito via AJAX
        /// </summary>
        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> EliminarAjax(long itemId)
        {
            try
            {
                await _carritoService.DeleteItemAsync(CurrentUser!.Id, itemId);

                var total = await _carritoService.GetTotalAsync(CurrentUser.Id);
                var totalItems = await _carritoService.GetTotalItemsAsync(CurrentUser.Id);

                return Json(new
                {
                    success = true,
                    message = "Producto eliminado del carrito",
                    total = total,
                    totalFormateado = total.ToString("C"),
                    totalItems = totalItems,
                    carritoVacio = totalItems == 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Vaciar carrito via AJAX
        /// </summary>
        [HttpPost]
        [JwtAuthorize]
        public async Task<IActionResult> VaciarAjax()
        {
            try
            {
                await _carritoService.ClearCarritoAsync(CurrentUser!.Id);

                return Json(new
                {
                    success = true,
                    message = "Carrito vaciado",
                    totalItems = 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Obtener resumen del carrito via AJAX
        /// </summary>
        [HttpGet]
        [JwtAuthorize]
        public async Task<IActionResult> GetResumenAjax()
        {
            try
            {
                var items = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                var total = await _carritoService.GetTotalAsync(CurrentUser.Id);
                var totalItems = await _carritoService.GetTotalItemsAsync(CurrentUser.Id);

                var itemsData = items.Select(i => new
                {
                    id = i.Id,
                    productoId = i.ProductoId,
                    nombre = i.ProductoNombre,
                    precio = i.Precio,
                    cantidad = i.Cantidad,
                    subtotal = i.Subtotal
                });

                return Json(new
                {
                    success = true,
                    items = itemsData,
                    total = total,
                    totalFormateado = total.ToString("C"),
                    totalItems = totalItems
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}
