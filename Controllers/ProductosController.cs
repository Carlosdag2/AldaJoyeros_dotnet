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
        private readonly AldaJoyeros.Services.CatalogoService _catalogo;
        private readonly ICategoriaService _categoriaService;
        private readonly ICompositeViewEngine _viewEngine;
        private const int PageSize = 12;

        public ProductosController(
            AldaJoyeros.Services.CatalogoService catalogo,
            ICategoriaService categoriaService,
            ICompositeViewEngine viewEngine)
        {
            _catalogo = catalogo;
            _categoriaService = categoriaService;
            _viewEngine = viewEngine;
        }

        public async Task<IActionResult> Index(long? categoriaId, string busqueda = "", int page = 1)
        {
            // Una búsqueda de texto siempre consulta todo el catálogo.
            if (!string.IsNullOrWhiteSpace(busqueda)) categoriaId = null;
            var categorias = await _categoriaService.GetAllAsync();
            var allCatalog = await _catalogo.GetAsync();
            foreach (var c in categorias) c.CantidadProductos = allCatalog.Count(p => p.CategoriaId == c.Id);
            ViewBag.TotalCatalogProducts = allCatalog.Count;
            foreach (var categoria in categorias.Where(c => !c.Nombre.Contains(" · ")))
                categoria.CantidadProductos += categorias.Where(c => c.Nombre.StartsWith(categoria.Nombre + " · ", StringComparison.Ordinal)).Sum(c => c.CantidadProductos);
            ViewBag.Categorias = categorias.Where(c => c.CantidadProductos > 0 && c.Grupo != "Pendientes de organizar")
                .OrderBy(c => AldaJoyeros.Catalog.CatalogTaxonomy.GroupOrder(c.Grupo)).ThenBy(c => c.Nombre).ToArray();
            ViewBag.CategoriaSeleccionada = categoriaId;
            ViewBag.Busqueda = busqueda;

            var productos = await _catalogo.GetAsync(categoriaId, busqueda);

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productos, page, PageSize);
            await _catalogo.LoadImagesAsync(pagedResult.Items);

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
            var productos = await _catalogo.GetAsync(categoriaId, busqueda);

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productos, page, PageSize);
            await _catalogo.LoadImagesAsync(pagedResult.Items);

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

            var productos = await _catalogo.GetAsync(busqueda: q, limite: Math.Clamp(limite, 1, 20));
            await _catalogo.LoadImagesAsync(productos);

            var sugerencias = productos.Select(p => new
            {
                id = p.Id,
                nombre = p.Nombre,
                categoria = p.CategoriaNombre,
                precio = p.Precio,
                precioFormateado = (p.PrecioDesde ? "Desde " : "") + p.Precio.ToString("C"),
                imagen = p.ImagenPrincipal,
                tieneImagen = p.TieneImagenes
            }).ToList();

            // Log para depuración (puedes quitar esto después)
            System.Diagnostics.Debug.WriteLine($"Búsqueda: '{q}', Encontrados: {sugerencias.Count}, Límite: {limite}");

            return Json(new { sugerencias });
        }

        public async Task<IActionResult> Detalle(long id, long? tallaId = null)
        {
            var producto = await _catalogo.DetailAsync(id, tallaId);
            if (producto == null)
            {
                Response.StatusCode = 404;
                return View("~/Views/Home/NotFoundPage.cshtml");
            }

            ViewBag.ProductosRelacionados = await _catalogo.RelatedAsync(producto);
            return View(producto);
        }
    }
}
