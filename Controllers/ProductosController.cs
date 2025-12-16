using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class ProductosController : BaseController
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private const int PageSize = 12;

        public ProductosController(IProductoService productoService, ICategoriaService categoriaService)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
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

            return View(pagedResult);
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
