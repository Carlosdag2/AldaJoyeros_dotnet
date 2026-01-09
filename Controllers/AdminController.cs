using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [AdminOnly]
    public class AdminController : BaseController
    {
        private readonly ICategoriaService _categoriaService;
        private readonly IProductoService _productoService;
        private readonly IPedidoService _pedidoService;
        private readonly IUsuarioService _usuarioService;
        private readonly IMetricasService _metricasService;
        private readonly ITraficoService _traficoService;

        public AdminController(
            ICategoriaService categoriaService,
            IProductoService productoService,
            IPedidoService pedidoService,
            IUsuarioService usuarioService,
            IMetricasService metricasService,
            ITraficoService traficoService)
        {
            _categoriaService = categoriaService;
            _productoService = productoService;
            _pedidoService = pedidoService;
            _usuarioService = usuarioService;
            _metricasService = metricasService;
            _traficoService = traficoService;
        }

        public async Task<IActionResult> Index()
        {
            // Obtener estadísticas
            var categorias = await _categoriaService.GetAllAsync();
            var productos = await _productoService.GetAllAsync();
            var pedidos = await _pedidoService.GetAllAsync();
            var usuarios = await _usuarioService.GetAllAsync();

            // Pasar estadísticas a la vista
            ViewBag.TotalCategorias = categorias.Count();
            ViewBag.TotalProductos = productos.Count();
            ViewBag.TotalPedidos = pedidos.Count();
            ViewBag.TotalUsuarios = usuarios.Count();

            return View();
        }

        public async Task<IActionResult> Metricas()
        {
            var metricas = await _metricasService.GetMetricasAsync();
            
            // Datos de tráfico en tiempo real
            ViewBag.VisitantesActivos = _traficoService.ObtenerVisitantesActivos();
            ViewBag.VisitantesPorPagina = _traficoService.ObtenerVisitantesPorPagina();
            
            return View(metricas);
        }

        [HttpGet]
        public IActionResult ObtenerTrafico()
        {
            var visitantesActivos = _traficoService.ObtenerVisitantesActivos();
            var visitantesPorPagina = _traficoService.ObtenerVisitantesPorPagina();
            
            return Json(new { 
                visitantesActivos, 
                visitantesPorPagina 
            });
        }
    }
}
