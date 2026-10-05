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
        private readonly AldaJoyeros.Services.FeaturedProductsStore _featured;

        public HomeController(IProductoService productoService, ICategoriaService categoriaService, AldaJoyeros.Services.FeaturedProductsStore featured)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _featured = featured;
        }

        public async Task<IActionResult> Index()
        {
            var ids = await _featured.GetIdsAsync();
            var active = (await _productoService.GetAllAsync(cargarImagenes: false)).ToDictionary(p => p.Id);
            var productos = ids.Where(active.ContainsKey).Select(id => active[id]).ToList();
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
