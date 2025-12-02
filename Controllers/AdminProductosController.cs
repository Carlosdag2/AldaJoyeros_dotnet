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

        public async Task<IActionResult> Index(string busqueda = "", int page = 1)
        {
            var productos = await _productoService.GetAllAsync();
            
            // Guardar total antes de aplicar filtro de búsqueda
            var totalProductos = productos.Count();
            
            // Aplicar búsqueda si hay término
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productos = productos.Where(p => 
                    p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    (p.Descripcion != null && p.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase)) ||
                    p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString().Contains(busqueda)
                ).ToList();
            }

            var pagedResult = PagedResult<ProductoDto>.Create(productos, page, PageSize);
            ViewBag.Busqueda = busqueda;
            ViewBag.TotalProductos = totalProductos;

            return View(pagedResult);
        }

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Categorias = await _categoriaService.GetAllAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(ProductoCreateDto productoDto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                return View(productoDto);
            }

            try
            {
                await _productoService.CreateAsync(productoDto);
                TempData["Success"] = "Producto creado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
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
                return NotFound();
            }

            var updateDto = new ProductoUpdateDto
            {
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                CategoriaId = producto.CategoriaId
            };

            ViewBag.Categorias = await _categoriaService.GetAllAsync();
            // Pasar la cantidad de imágenes al ViewBag
            ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
            
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, ProductoUpdateDto productoDto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                // Recuperar la cantidad de imágenes también en caso de error
                var producto = await _productoService.GetByIdAsync(id);
                if (producto != null)
                {
                    ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
                }
                return View(productoDto);
            }

            try
            {
                await _productoService.UpdateAsync(id, productoDto);
                TempData["Success"] = "Producto actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.Categorias = await _categoriaService.GetAllAsync();
                // Recuperar la cantidad de imágenes también en caso de error
                var producto = await _productoService.GetByIdAsync(id);
                if (producto != null)
                {
                    ViewBag.Imagenes = producto.Imagenes?.Count ?? 0;
                }
                return View(productoDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                await _productoService.DeleteAsync(id);
                TempData["Success"] = "Producto eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
