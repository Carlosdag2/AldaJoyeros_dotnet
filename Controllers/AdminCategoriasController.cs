using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Attributes;
using System.Linq;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
    public class AdminCategoriasController : BaseController
    {
        private readonly ICategoriaService _categoriaService;

        public AdminCategoriasController(ICategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
        }

        public async Task<IActionResult> Index()
        {
            var categorias = await _categoriaService.GetAllAsync();
            
            // Calcular el total de productos
            ViewBag.TotalProductos = categorias.Sum(c => c.CantidadProductos);

            return View(categorias);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(CategoriaCreateDto categoriaDto)
        {
            if (!ModelState.IsValid)
            {
                return View(categoriaDto);
            }

            try
            {
                await _categoriaService.CreateAsync(categoriaDto);
                TempData["Success"] = "Categoría creada exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(categoriaDto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(long id)
        {
            var categoria = await _categoriaService.GetByIdAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }

            var updateDto = new CategoriaUpdateDto
            {
                Nombre = categoria.Nombre
            };

            // Pasar datos al ViewBag para la vista
            ViewBag.CategoriaId = id;
            ViewBag.CantidadProductos = categoria.CantidadProductos;

            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, CategoriaUpdateDto categoriaDto)
        {
            if (!ModelState.IsValid)
            {
                // Si hay errores, recuperar los datos para el ViewBag
                var categoria = await _categoriaService.GetByIdAsync(id);
                if (categoria != null)
                {
                    ViewBag.CategoriaId = id;
                    ViewBag.CantidadProductos = categoria.CantidadProductos;
                }
                return View(categoriaDto);
            }

            try
            {
                await _categoriaService.UpdateAsync(id, categoriaDto);
                TempData["Success"] = "Categoría actualizada exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                // Recuperar los datos para el ViewBag en caso de error
                var categoria = await _categoriaService.GetByIdAsync(id);
                if (categoria != null)
                {
                    ViewBag.CategoriaId = id;
                    ViewBag.CantidadProductos = categoria.CantidadProductos;
                }
                return View(categoriaDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                await _categoriaService.DeleteAsync(id);
                TempData["Success"] = "Categoría eliminada exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
