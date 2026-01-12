using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class ProductosController : BaseController
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private readonly ICompositeViewEngine _viewEngine;
        private const int PageSize = 12;

        public ProductosController(
            IProductoService productoService, 
            ICategoriaService categoriaService,
            ICompositeViewEngine viewEngine)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _viewEngine = viewEngine;
        }

        public async Task<IActionResult> Index(long? categoriaId, string busqueda = "", int page = 1)
        {
            var categorias = await _categoriaService.GetAllAsync();
            ViewBag.Categorias = categorias;
            ViewBag.CategoriaSeleccionada = categoriaId;
            ViewBag.Busqueda = busqueda;

            IEnumerable<DTOs.ProductoDto> productos;
            
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productos = await _productoService.BuscarAsync(busqueda, categoriaId);
            }
            else
            {
                productos = categoriaId.HasValue
                    ? await _productoService.GetByCategoriaAsync(categoriaId.Value)
                    : await _productoService.GetAllAsync();
            }

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productos, page, PageSize);

            // Si es petición AJAX, devolver solo el partial
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return PartialView("_ProductosGrid", pagedResult);
            }

            return View(pagedResult);
        }

        /// <summary>
        /// Endpoint AJAX para filtrar productos con JSON response
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Filtrar(long? categoriaId, string busqueda = "", int page = 1)
        {
            IEnumerable<DTOs.ProductoDto> productos;
            
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productos = await _productoService.BuscarAsync(busqueda, categoriaId);
            }
            else
            {
                productos = categoriaId.HasValue
                    ? await _productoService.GetByCategoriaAsync(categoriaId.Value)
                    : await _productoService.GetAllAsync();
            }

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productos, page, PageSize);

            return Json(new
            {
                success = true,
                html = await RenderPartialViewToStringAsync("_ProductosGrid", pagedResult),
                totalItems = pagedResult.TotalItems,
                currentPage = pagedResult.PageNumber,
                totalPages = pagedResult.TotalPages,
                startItem = pagedResult.StartItem,
                endItem = pagedResult.EndItem
            });
        }

        /// <summary>
        /// Renderiza un partial view a string para respuestas AJAX
        /// </summary>
        private async Task<string> RenderPartialViewToStringAsync<TModel>(string viewName, TModel model)
        {
            ViewData.Model = model;
            using var writer = new StringWriter();
            var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);
            if (!viewResult.Success)
            {
                return $"Vista '{viewName}' no encontrada";
            }
            var viewContext = new ViewContext(
                ControllerContext,
                viewResult.View,
                ViewData,
                TempData,
                writer,
                new HtmlHelperOptions()
            );
            await viewResult.View.RenderAsync(viewContext);
            return writer.ToString();
        }

        /// <summary>
        /// Endpoint para búsqueda dinámica con sugerencias
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Buscar(string q, long? categoriaId, int limite = 5)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new { sugerencias = Array.Empty<object>() });
            }

            var productos = await _productoService.BuscarAsync(q, categoriaId, limite);

            var sugerencias = productos.Select(p => new
            {
                id = p.Id,
                nombre = p.Nombre,
                categoria = p.CategoriaNombre,
                precio = p.Precio,
                precioFormateado = p.Precio.ToString("C"),
                imagen = p.ImagenPrincipal,
                tieneImagen = p.TieneImagenes
            }).ToList();

            // Log para depuración (puedes quitar esto después)
            System.Diagnostics.Debug.WriteLine($"Búsqueda: '{q}', Encontrados: {sugerencias.Count}, Límite: {limite}");

            return Json(new { sugerencias });
        }

        public async Task<IActionResult> Detalle(long id)
        {
            var producto = await _productoService.GetByIdActiveAsync(id);
            if (producto == null)
            {
                TempData["Error"] = "Este producto no está disponible";
                return RedirectToAction("Index");
            }

            return View(producto);
        }
    }
}
