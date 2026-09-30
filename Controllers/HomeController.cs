using System.Diagnostics;
using AldaJoyeros.Models;
using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;

        public HomeController(IProductoService productoService, ICategoriaService categoriaService)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
        }

        public async Task<IActionResult> Index()
        {
            var productos = (await _productoService.GetAllAsync(cargarImagenes: false)).Take(8).ToList();
            await _productoService.CargarImagenesAsync(productos);
            var categorias = await _categoriaService.GetAllAsync();
            
            ViewBag.Categorias = categorias.Where(c => c.CantidadProductos > 0 && c.Grupo != "Pendientes de organizar")
                .OrderBy(c => AldaJoyeros.Catalog.CatalogTaxonomy.GroupOrder(c.Grupo)).ThenBy(c => c.Nombre).ToArray();
            return View(productos);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terminos()
        {
            return View();
        }

        public IActionResult Cookies()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
