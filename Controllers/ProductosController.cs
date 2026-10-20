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

        public async Task<IActionResult> Index(long? categoriaId, string busqueda = "", int page = 1, string? categoria = null)
        {
            // Una búsqueda de texto siempre consulta todo el catálogo.
            if (!string.IsNullOrWhiteSpace(busqueda)) categoriaId = null;
            var categorias = await _categoriaService.GetAllAsync();
            await _catalogo.AssignCategoryUrlsAsync(categorias);
            if (!string.IsNullOrWhiteSpace(categoria) && string.IsNullOrWhiteSpace(busqueda))
            {
                var match = categorias.FirstOrDefault(c => c.UrlSlug.Equals(categoria, StringComparison.OrdinalIgnoreCase));
                if (match == null) { Response.StatusCode = 404; return View("~/Views/Home/NotFoundPage.cshtml"); }
                categoriaId = match.Id;
            }
            if (categoriaId.HasValue && !categorias.Any(c => c.Id == categoriaId)) { Response.StatusCode = 404; return View("~/Views/Home/NotFoundPage.cshtml"); }
            var allCatalog = await _catalogo.GetAsync();
            foreach (var c in categorias) c.CantidadProductos = allCatalog.Count(p => p.CategoriaId == c.Id);
            ViewBag.TotalCatalogProducts = allCatalog.Count;
            foreach (var categoryRow in categorias.Where(c => !c.Nombre.Contains(" · ")))
                categoryRow.CantidadProductos += categorias.Where(c => c.Nombre.StartsWith(categoryRow.Nombre + " · ", StringComparison.Ordinal)).Sum(c => c.CantidadProductos);
            ViewBag.Categorias = categorias.Where(c => c.CantidadProductos > 0 && c.Grupo != "Pendientes de organizar")
                .OrderBy(c => AldaJoyeros.Catalog.CatalogTaxonomy.GroupOrder(c.Grupo)).ThenBy(c => c.Nombre).ToArray();
            ViewBag.CategoriaSeleccionada = categoriaId;
            ViewBag.Busqueda = busqueda;

            var productos = await _catalogo.GetAsync(categoriaId, busqueda);

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productos, page, PageSize);
            var categorySlug = categorias.FirstOrDefault(c => c.Id == categoriaId)?.UrlSlug;
            ViewBag.CategoriaSlug = categorySlug;
            var path = PublicUrls.CatalogPath(categorySlug);
            var publication = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.PublicationOptions>>().Value;
            if (Request.Headers.XRequestedWith != "XMLHttpRequest" &&
                (!string.Equals(Request.Path.Value, path, StringComparison.Ordinal) || Request.Query.ContainsKey("categoriaId") || PublicUrls.ShouldRedirectOrigin(Request, publication)))
                return RedirectPermanent(PublicUrls.Destination(Request, publication, path, PublicUrls.CatalogQuery(Request, busqueda, pagedResult.PageNumber)));
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
                url = PublicUrls.ProductLink(Request, p),
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

        public async Task<IActionResult> Detalle(long id = 0, long? tallaId = null, string? slug = null, string? talla = null)
        {
            var namedRoute = !string.IsNullOrEmpty(RouteData.Values["slug"]?.ToString());
            var producto = namedRoute ? await _catalogo.DetailBySlugAsync(slug!, talla) : id > 0 ? await _catalogo.DetailAsync(id, tallaId) : null;
            if (producto != null && !string.IsNullOrEmpty(talla)) producto = await _catalogo.DetailBySlugAsync(producto.UrlSlug, talla);
            else if (producto != null && namedRoute && tallaId.HasValue) producto = await _catalogo.DetailAsync(producto.Id, tallaId);
            if (producto == null)
            {
                Response.StatusCode = 404;
                return View("~/Views/Home/NotFoundPage.cshtml");
            }

            var publication = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.PublicationOptions>>().Value;
            var path = PublicUrls.ProductPath(producto);
            if ((HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method)) &&
                (!string.Equals(Request.Path.Value, path, StringComparison.Ordinal) || Request.Query.ContainsKey("id") || Request.Query.ContainsKey("tallaId") || Request.Query.ContainsKey("talla") && Request.Query["talla"] != PublicUrls.SelectedSize(producto) || PublicUrls.ShouldRedirectOrigin(Request, publication)))
                return RedirectPermanent(PublicUrls.Destination(Request, publication, path, PublicUrls.ProductQuery(Request, producto)));

            ViewBag.ProductosRelacionados = await _catalogo.RelatedAsync(producto);
            return View(producto);
        }
    }
}
