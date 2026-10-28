using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
    [AutoValidateAntiforgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

        public async Task<IActionResult> Index(string busqueda = "", string filtro = "activos", int page = 1, long? categoriaId = null, string orden = "recientes", int tamano = 20)
        {
            busqueda = (busqueda ?? "").Trim();
            filtro = filtro is "eliminados" or "todos" ? filtro : "activos";
            tamano = tamano is 10 or 20 or 50 ? tamano : 20;
            orden = orden is "nombre" or "precio-asc" or "precio-desc" ? orden : "recientes";
            // Obtener todos los productos incluyendo eliminados para admin
            var productos = await _productoService.GetAllIncludingDeletedAsync(cargarImagenes: false);
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

            if (categoriaId.HasValue) productosFiltrados = productosFiltrados.Where(p => p.CategoriaId == categoriaId);
            productosFiltrados = orden switch
            {
                "nombre" => productosFiltrados.OrderBy(p => p.Nombre).ThenBy(p => p.Id),
                "precio-asc" => productosFiltrados.OrderBy(p => p.Precio).ThenBy(p => p.Id),
                "precio-desc" => productosFiltrados.OrderByDescending(p => p.Precio).ThenBy(p => p.Id),
                _ => productosFiltrados.OrderByDescending(p => p.Id)
            };
            var pagedResult = PagedResult<ProductoDto>.Create(productosFiltrados, page, tamano);
            await _productoService.CargarImagenesAsync(pagedResult.Items);
            ViewBag.Busqueda = busqueda;
            ViewBag.Filtro = filtro;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.Orden = orden;
            ViewBag.Tamano = tamano;

            // Si es petición AJAX, devolver partial con datos de estadísticas en el response header
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                Response.Headers.Append("X-Stats-Activos", totalActivos.ToString());
                Response.Headers.Append("X-Stats-Eliminados", totalEliminados.ToString());
                Response.Headers.Append("X-Stats-Total", pagedResult.TotalItems.ToString());
                Response.Headers.Append("X-Stats-Pagina", $"{pagedResult.PageNumber}/{pagedResult.TotalPages}");
                return PartialView("_ProductosList", pagedResult);
            }

            ViewBag.Categorias = (await _categoriaService.GetAllAsync()).OrderBy(c => c.Nombre).ToList();
            return View(pagedResult);
        }

        [HttpGet]
        public async Task<IActionResult> Editor(long id = 0)
        {
            var producto = id == 0 ? null : await _productoService.GetByIdAsync(id);
            if (id != 0 && producto == null) return NotFound();
            if (producto?.Eliminado == true) return Conflict("Restaura el producto antes de editarlo.");
            ViewBag.ProductoId = id;
            ViewBag.Categorias = (await _categoriaService.GetAllAsync()).OrderBy(c => c.Nombre).ToList();
            return PartialView("_ProductoEditor", new ProductoUpdateDto {
                Nombre = producto?.Nombre ?? "", Descripcion = producto?.Descripcion,
                Precio = producto?.Precio ?? 0, CategoriaId = producto?.CategoriaId ?? 0
            });
        }

        [HttpPost]
        public async Task<IActionResult> Guardar(long id, ProductoUpdateDto productoDto)
        {
            var categorias = await _categoriaService.GetAllAsync();
            if (!categorias.Any(c => c.Id == productoDto.CategoriaId)) ModelState.AddModelError("CategoriaId", "Selecciona una categoría válida.");
            if (!double.IsFinite(productoDto.Precio)) ModelState.AddModelError("Precio", "Introduce un precio válido.");
            if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Revisa los campos indicados.", errors = ModelState.Where(x => x.Value!.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Valor no válido." : e.ErrorMessage).ToArray()) });
            try
            {
                if (id == 0)
                {
                    var created = await _productoService.CreateAsync(new ProductoCreateDto { Nombre = productoDto.Nombre, Descripcion = productoDto.Descripcion, Precio = productoDto.Precio, CategoriaId = productoDto.CategoriaId });
                    return Json(new { success = true, id = created.Id, message = "Producto creado. Puedes añadir sus fotos desde Imágenes." });
                }
                var current = await _productoService.GetByIdAsync(id);
                if (current == null) return NotFound(new { message = "El producto ya no existe." });
                if (current.Eliminado) return Conflict(new { message = "El producto está en la papelera. Restáuralo antes de editarlo." });
                await _productoService.UpdateAsync(id, productoDto);
                return Json(new { success = true, id, message = "Producto actualizado." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "No se ha podido guardar. Revisa la lista antes de intentarlo de nuevo." });
            }
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
            var productos = await _productoService.GetAllIncludingDeletedAsync(cargarImagenes: false);
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
            await _productoService.CargarImagenesAsync(pagedResult.Items);

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
