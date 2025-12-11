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
            var totalProductos = productos.Count();
            
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                productos = productos.Where(p => 
                    p.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    (p.Descripcion != null && p.Descripcion.Contains(busqueda, StringComparison.OrdinalIgnoreCase)) ||
                    p.CategoriaNombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                    p.Id.ToString().Contains(busqueda)
                ).ToList();
                
                if (!productos.Any())
                {
                    TempData["Info"] = $"No se encontraron productos con '{busqueda}'";
                }
            }

            var pagedResult = PagedResult<ProductoDto>.Create(productos, page, PageSize);
            ViewBag.Busqueda = busqueda;
            ViewBag.TotalProductos = totalProductos;

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
                TempData["Success"] = $"Producto '{nombreProducto}' eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar el producto: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
