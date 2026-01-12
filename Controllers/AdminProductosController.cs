using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
    public class AdminProductosController : BaseController
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private const int PageSize = 10;

        public AdminProductosController(IProductoService productoService, ICategoriaService categoriaService)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
        }

        public async Task<IActionResult> Index(string busqueda = "", string filtro = "activos", int page = 1)
        {
            // Obtener todos los productos incluyendo eliminados para admin
            var productos = await _productoService.GetAllIncludingDeletedAsync();
            var todosProductos = productos.ToList();
            
            // Estadísticas
            var totalActivos = todosProductos.Count(p => !p.Eliminado);
            var totalEliminados = todosProductos.Count(p => p.Eliminado);
            
            ViewBag.TotalProductos = totalActivos;
            ViewBag.TotalEliminados = totalEliminados;
            
            // Aplicar filtro de estado
            IEnumerable<ProductoDto> productosFiltrados = filtro switch
            {
                "eliminados" => todosProductos.Where(p => p.Eliminado),
                "todos" => todosProductos,
                _ => todosProductos.Where(p => !p.Eliminado) // activos por defecto
            };
            
            // Aplicar búsqueda
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productosFiltrados = productosFiltrados.Where(p => 
                    p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    (p.Descripcion != null && p.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase)) ||
                    p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString().Contains(busqueda)
                ).ToList();
            }

            var pagedResult = PagedResult<ProductoDto>.Create(productosFiltrados, page, PageSize);
            ViewBag.Busqueda = busqueda;
            ViewBag.Filtro = filtro;

            // Si es petición AJAX, devolver partial con datos de estadísticas en el response header
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                Response.Headers.Append("X-Stats-Activos", totalActivos.ToString());
                Response.Headers.Append("X-Stats-Eliminados", totalEliminados.ToString());
                Response.Headers.Append("X-Stats-Total", pagedResult.TotalItems.ToString());
                Response.Headers.Append("X-Stats-Pagina", $"{pagedResult.PageNumber}/{pagedResult.TotalPages}");
                return PartialView("_ProductosList", pagedResult);
            }

            return View(pagedResult);
        }

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var categorias = await _categoriaService.GetAllAsync();
            if (!categorias.Any())
            {
                TempData["Warning"] = "Debes crear al menos una categoría antes de añadir productos";
                return RedirectToAction("Index", "AdminCategorias");
            }
            
            ViewBag.Categorias = categorias;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(ProductoCreateDto productoDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                return View(productoDto);
            }

            try
            {
                var producto = await _productoService.CreateAsync(productoDto);
                TempData["Success"] = $"Producto '{productoDto.Nombre}' creado exitosamente. ¡Ahora puedes añadir imágenes!";
                return RedirectToAction("Gestionar", "AdminImagenes", new { id = producto.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear el producto: {ex.Message}";
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                return View(productoDto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(long id)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null)
            {
                TempData["Error"] = "Producto no encontrado";
                return RedirectToAction("Index");
            }

            if (producto.Eliminado)
            {
                TempData["Warning"] = "Este producto está eliminado. Restáuralo primero para editarlo.";
            }

            var updateDto = new ProductoUpdateDto
            {
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                CategoriaId = producto.CategoriaId
            };

            ViewBag.Categorias = await _categoriaService.GetAllAsync();
            ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
            ViewBag.ProductoId = id;
            ViewBag.Eliminado = producto.Eliminado;
            
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, ProductoUpdateDto productoDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                var producto = await _productoService.GetByIdAsync(id);
                if (producto != null)
                {
                    ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
                    ViewBag.ProductoId = id;
                    ViewBag.Eliminado = producto.Eliminado;
                }
                return View(productoDto);
            }

            try
            {
                await _productoService.UpdateAsync(id, productoDto);
                TempData["Success"] = $"Producto '{productoDto.Nombre}' actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar el producto: {ex.Message}";
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                var producto = await _productoService.GetByIdAsync(id);
                if (producto != null)
                {
                    ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
                    ViewBag.ProductoId = id;
                    ViewBag.Eliminado = producto.Eliminado;
                }
                return View(productoDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                var nombreProducto = producto?.Nombre ?? "El producto";
                
                await _productoService.DeleteAsync(id);
                TempData["Success"] = $"Producto '{nombreProducto}' eliminado. Puedes restaurarlo desde la papelera.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar el producto: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Restaurar(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                var nombreProducto = producto?.Nombre ?? "El producto";
                
                await _productoService.RestoreAsync(id);
                TempData["Success"] = $"Producto '{nombreProducto}' restaurado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo restaurar el producto: {ex.Message}";
            }

            return RedirectToAction("Index", new { filtro = "eliminados" });
        }

        [HttpPost]
        public async Task<IActionResult> EliminarPermanente(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                
                if (producto != null && !producto.Eliminado)
                {
                    TempData["Error"] = "Solo puedes eliminar permanentemente productos que estén en la papelera";
                    return RedirectToAction("Index");
                }
                
                var nombreProducto = producto?.Nombre ?? "El producto";
                
                await _productoService.HardDeleteAsync(id);
                TempData["Success"] = $"Producto '{nombreProducto}' eliminado permanentemente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar el producto: {ex.Message}";
            }

            return RedirectToAction("Index", new { filtro = "eliminados" });
        }

        #region API AJAX

        /// <summary>
        /// Eliminar producto via AJAX (soft delete)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarAjax(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                if (producto == null)
                {
                    return Json(new { success = false, message = "Producto no encontrado" });
                }

                await _productoService.DeleteAsync(id);
                
                return Json(new { 
                    success = true, 
                    message = $"'{producto.Nombre}' movido a la papelera",
                    productoNombre = producto.Nombre
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Restaurar producto via AJAX
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RestaurarAjax(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                if (producto == null)
                {
                    return Json(new { success = false, message = "Producto no encontrado" });
                }

                await _productoService.RestoreAsync(id);
                
                return Json(new { 
                    success = true, 
                    message = $"'{producto.Nombre}' restaurado",
                    productoNombre = producto.Nombre
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar permanentemente via AJAX
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarPermanenteAjax(long id)
        {
            try
            {
                var producto = await _productoService.GetByIdAsync(id);
                if (producto == null)
                {
                    return Json(new { success = false, message = "Producto no encontrado" });
                }

                if (!producto.Eliminado)
                {
                    return Json(new { success = false, message = "El producto debe estar en la papelera primero" });
                }

                await _productoService.HardDeleteAsync(id);
                
                return Json(new { 
                    success = true, 
                    message = $"'{producto.Nombre}' eliminado permanentemente"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Buscar productos via AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarAjax(string busqueda = "", string filtro = "activos", int page = 1)
        {
            var productos = await _productoService.GetAllIncludingDeletedAsync();
            var todosProductos = productos.ToList();
            
            IEnumerable<ProductoDto> productosFiltrados = filtro switch
            {
                "eliminados" => todosProductos.Where(p => p.Eliminado),
                "todos" => todosProductos,
                _ => todosProductos.Where(p => !p.Eliminado)
            };
            
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productosFiltrados = productosFiltrados.Where(p => 
                    p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    (p.Descripcion != null && p.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase)) ||
                    p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString().Contains(busqueda)
                ).ToList();
            }

            var pagedResult = PagedResult<ProductoDto>.Create(productosFiltrados, page, PageSize);

            var productosData = pagedResult.Items.Select(p => new
            {
                id = p.Id,
                nombre = p.Nombre,
                precio = p.Precio,
                precioFormateado = p.Precio.ToString("C"),
                categoria = p.CategoriaNombre,
                imagen = p.ImagenPrincipal,
                tieneImagenes = p.TieneImagenes,
                eliminado = p.Eliminado
            });

            return Json(new
            {
                success = true,
                productos = productosData,
                currentPage = pagedResult.PageNumber,
                totalPages = pagedResult.TotalPages,
                totalItems = pagedResult.TotalItems,
                totalActivos = todosProductos.Count(p => !p.Eliminado),
                totalEliminados = todosProductos.Count(p => p.Eliminado)
            });
        }

        #endregion
    }
}
