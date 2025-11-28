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

            var productosQuery = categoriaId.HasValue
                ? await _productoService.GetByCategoriaAsync(categoriaId.Value)
                : await _productoService.GetAllAsync();

            // Aplicar búsqueda si hay término
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productosQuery = productosQuery.Where(p => 
                    p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            var pagedResult = PagedResult<DTOs.ProductoDto>.Create(productosQuery, page, PageSize);

            return View(pagedResult);
        }

        public async Task<IActionResult> Detalle(long id)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }
    }
}
