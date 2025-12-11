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
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                return View(categoriaDto);
            }

            // No permitir crear categoría con nombre reservado
            if (categoriaDto.Nombre.Trim().Equals("Sin categoría", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "El nombre 'Sin categoría' está reservado para el sistema";
                return View(categoriaDto);
            }

            try
            {
                await _categoriaService.CreateAsync(categoriaDto);
                TempData["Success"] = $"Categoría '{categoriaDto.Nombre}' creada exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear la categoría: {ex.Message}";
                return View(categoriaDto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(long id)
        {
            var categoria = await _categoriaService.GetByIdAsync(id);
            if (categoria == null)
            {
                TempData["Error"] = "Categoría no encontrada";
                return RedirectToAction("Index");
            }

            // Verificar si es la categoría por defecto
            if (await _categoriaService.IsDefaultCategoryAsync(id))
            {
                TempData["Warning"] = "Esta es la categoría por defecto del sistema. No se puede renombrar ni eliminar.";
            }

            var updateDto = new CategoriaUpdateDto
            {
                Nombre = categoria.Nombre
            };

            ViewBag.CategoriaId = id;
            ViewBag.CantidadProductos = categoria.CantidadProductos;
            ViewBag.IsDefaultCategory = categoria.Nombre == "Sin categoría";

            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, CategoriaUpdateDto categoriaDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                var categoria = await _categoriaService.GetByIdAsync(id);
                if (categoria != null)
                {
                    ViewBag.CategoriaId = id;
                    ViewBag.CantidadProductos = categoria.CantidadProductos;
                    ViewBag.IsDefaultCategory = categoria.Nombre == "Sin categoría";
                }
                return View(categoriaDto);
            }

            try
            {
                await _categoriaService.UpdateAsync(id, categoriaDto);
                TempData["Success"] = $"Categoría '{categoriaDto.Nombre}' actualizada exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar la categoría: {ex.Message}";
                var categoria = await _categoriaService.GetByIdAsync(id);
                if (categoria != null)
                {
                    ViewBag.CategoriaId = id;
                    ViewBag.CantidadProductos = categoria.CantidadProductos;
                    ViewBag.IsDefaultCategory = categoria.Nombre == "Sin categoría";
                }
                return View(categoriaDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                var categoria = await _categoriaService.GetByIdAsync(id);
                if (categoria == null)
                {
                    TempData["Error"] = "Categoría no encontrada";
                    return RedirectToAction("Index");
                }

                var nombreCategoria = categoria.Nombre;
                var cantidadProductos = categoria.CantidadProductos;
                
                await _categoriaService.DeleteAsync(id);
                
                if (cantidadProductos > 0)
                {
                    TempData["Success"] = $"Categoría '{nombreCategoria}' eliminada. Sus {cantidadProductos} producto(s) fueron movidos a 'Sin categoría'.";
                }
                else
                {
                    TempData["Success"] = $"Categoría '{nombreCategoria}' eliminada exitosamente";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar la categoría: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
