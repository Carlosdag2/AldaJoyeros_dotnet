using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize]
    public class PedidosController : BaseController
    {
        private readonly IPedidoService _pedidoService;
        private readonly ICarritoService _carritoService;
        private readonly IDireccionService _direccionService;
        private readonly IProductoImagenService _imagenService;
        private readonly IPaymentService _paymentService;

        public PedidosController(
            IPedidoService pedidoService,
            ICarritoService carritoService,
            IDireccionService direccionService,
            IProductoImagenService imagenService,
            IPaymentService paymentService)
        {
            _pedidoService = pedidoService;
            _carritoService = carritoService;
            _direccionService = direccionService;
            _imagenService = imagenService;
            _paymentService = paymentService;
        }

        public async Task<IActionResult> Index()
        {
            var pedidos = await _pedidoService.GetByUsuarioIdAsync(CurrentUser!.Id);
            
            foreach (var pedido in pedidos)
            {
                foreach (var linea in pedido.LineasPedido)
                {
                    if (linea.Producto != null && linea.ProductoId.HasValue)
                    {
                        var imagenes = await _imagenService.GetByProductoIdAsync(linea.ProductoId.Value);
                        linea.Producto.Imagenes = imagenes.ToList();
                    }
                }
            }
            
            return View(pedidos);
        }

        public async Task<IActionResult> Detalle(long id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null || pedido.UsuarioId != CurrentUser!.Id)
            {
                return NotFound();
            }

            foreach (var linea in pedido.LineasPedido)
            {
                if (linea.Producto != null && linea.ProductoId.HasValue)
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(linea.ProductoId.Value);
                    linea.Producto.Imagenes = imagenes.ToList();
                }
            }

            return View(pedido);
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            foreach (var item in carritoItems)
            {
                if (item.Producto != null)
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                    item.Producto.Imagenes = imagenes.ToList();
                }
            }

            var viewModel = new CheckoutViewModel
            {
                CarritoItems = carritoItems,
                Direccion = new DireccionDto(),
                MetodoPago = "Contra Reembolso"
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                foreach (var item in carritoItems)
                {
                    if (item.Producto != null)
                    {
                        var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                        item.Producto.Imagenes = imagenes.ToList();
                    }
                }
                viewModel.CarritoItems = carritoItems;
                return View(viewModel);
            }

            try
            {
                // Procesar pago si no es contra reembolso
                if (viewModel.MetodoPago != "Contra Reembolso")
                {
                    // Crear intención de pago
                    var paymentIntentId = await _paymentService.CreatePaymentIntentAsync((decimal)viewModel.Total, "EUR");
                    
                    // Procesar el pago
                    var paymentSuccess = await _paymentService.ProcessPaymentAsync(paymentIntentId, viewModel.MetodoPago);
                    
                    if (!paymentSuccess)
                    {
                        TempData["Error"] = "Error al procesar el pago. Por favor, inténtalo de nuevo.";
                        var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                        foreach (var item in carritoItems)
                        {
                            if (item.Producto != null)
                            {
                                var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                                item.Producto.Imagenes = imagenes.ToList();
                            }
                        }
                        viewModel.CarritoItems = carritoItems;
                        return View(viewModel);
                    }
                }

                // Crear pedido
                var pedidoDto = new PedidoCreateDto
                {
                    Direccion = new DireccionCreateDto
                    {
                        Calle = viewModel.Direccion.Calle,
                        Numero = viewModel.Direccion.Numero,
                        Piso = viewModel.Direccion.Piso,
                        CodigoPostal = viewModel.Direccion.CodigoPostal,
                        Ciudad = viewModel.Direccion.Ciudad,
                        Provincia = viewModel.Direccion.Provincia
                    }
                };

                var pedido = await _pedidoService.CreateFromCarritoAsync(CurrentUser!.Id, pedidoDto);
                
                TempData["Success"] = viewModel.MetodoPago == "Contra Reembolso" 
                    ? "Pedido realizado exitosamente. Pagarás al recibir tu pedido." 
                    : "Pedido realizado y pago procesado exitosamente.";
                
                return RedirectToAction("Detalle", new { id = pedido.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                foreach (var item in carritoItems)
                {
                    if (item.Producto != null)
                    {
                        var imagenes = await _imagenService.GetByProductoIdAsync(item.ProductoId);
                        item.Producto.Imagenes = imagenes.ToList();
                    }
                }
                viewModel.CarritoItems = carritoItems;
                return View(viewModel);
            }
        }
    }
}
