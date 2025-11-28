using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using System.Linq;

namespace AldaJoyeros.Controllers
{
    public class AdminCategoriasController : BaseController
    {
        private readonly ICategoriaService _categoriaService;

        public AdminCategoriasController(ICategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            var categorias = await _categoriaService.GetAllAsync();
            
            // Calcular el total de productos
            ViewBag.TotalProductos = categorias.Sum(c => c.CantidadProductos);

            return View(categorias);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(CategoriaCreateDto categoriaDto)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

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
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            var categoria = await _categoriaService.GetByIdAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }

            var updateDto = new CategoriaUpdateDto
            {
                Nombre = categoria.Nombre
            };

            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, CategoriaUpdateDto categoriaDto)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
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
                return View(categoriaDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

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
