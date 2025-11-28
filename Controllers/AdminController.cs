using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Controllers
{
    public class AdminController : BaseController
    {
        private readonly ICategoriaService _categoriaService;
        private readonly IProductoService _productoService;
        private readonly IPedidoService _pedidoService;
        private readonly IUsuarioService _usuarioService;

        public AdminController(
            ICategoriaService categoriaService,
            IProductoService productoService,
            IPedidoService pedidoService,
            IUsuarioService usuarioService)
        {
            _categoriaService = categoriaService;
            _productoService = productoService;
            _pedidoService = pedidoService;
            _usuarioService = usuarioService;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

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
    }
}
