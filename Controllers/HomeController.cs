using System.Diagnostics;
using AldaJoyeros.Models;
using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class HomeController : BaseController
    {
        private readonly AldaJoyeros.Services.CatalogoService _catalogo;
        private readonly ICategoriaService _categoriaService;
        private readonly AldaJoyeros.Services.FeaturedProductsStore _featured;
        private readonly AldaJoyeros.Services.Promotions.PromotionStore? _promotions;

        public HomeController(AldaJoyeros.Services.CatalogoService catalogo, ICategoriaService categoriaService, AldaJoyeros.Services.FeaturedProductsStore featured, AldaJoyeros.Services.Promotions.PromotionStore? promotions = null)
        {
            _catalogo = catalogo;
            _categoriaService = categoriaService;
            _featured = featured;
            _promotions = promotions;
        }

        public async Task<IActionResult> Index()
        {
            var ids = await _featured.GetIdsAsync();
            var families = await _catalogo.FamiliesAsync();
            var productos = ids.Select(id => families.FirstOrDefault(f => f.Members.Any(p => p.Id == id && !p.Eliminado))?.Product)
                .OfType<AldaJoyeros.DTOs.ProductoDto>().DistinctBy(p => p.Id).ToList();
            await _catalogo.LoadImagesAsync(productos);
            var categorias = await _categoriaService.GetAllAsync();
            await _catalogo.AssignCategoryUrlsAsync(categorias);
            foreach (var c in categorias) c.CantidadProductos = families.Count(f => f.Product.CategoriaId == c.Id);
            foreach (var parent in categorias.Where(c => !c.Nombre.Contains(" · ")))
                parent.CantidadProductos += categorias.Where(c => c.Nombre.StartsWith(parent.Nombre + " · ", StringComparison.Ordinal)).Sum(c => c.CantidadProductos);
            
            ViewBag.Categorias = categorias.Where(c => c.CantidadProductos > 0 && c.Grupo != "Pendientes de organizar")
                .OrderBy(c => AldaJoyeros.Catalog.CatalogTaxonomy.GroupOrder(c.Grupo)).ThenBy(c => c.Nombre).ToArray();
            if(_promotions != null) { var promotion = await _promotions.GetAsync();
                var offers = promotion.HomeEnabled ? promotion.HomeProducts.Select(id=>families.FirstOrDefault(f=>f.Product.Id==id)?.Product).Where(p=>p?.TieneDescuento==true).Cast<AldaJoyeros.DTOs.ProductoDto>().ToArray() : Array.Empty<AldaJoyeros.DTOs.ProductoDto>();
                await _catalogo.LoadImagesAsync(offers); ViewBag.HomeOffers=offers; }
            return View(productos);
        }

        public IActionResult AvisoLegal() => View();

        public IActionResult NotFoundPage()
        {
            Response.StatusCode = 404;
            return View();
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
