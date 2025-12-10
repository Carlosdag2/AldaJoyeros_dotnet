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
        private readonly ILogger<PedidosController> _logger;

        public PedidosController(
            IPedidoService pedidoService,
            ICarritoService carritoService,
            IDireccionService direccionService,
            IProductoImagenService imagenService,
            IPaymentService paymentService,
            ILogger<PedidosController> logger)
        {
            _pedidoService = pedidoService;
            _carritoService = carritoService;
            _direccionService = direccionService;
            _imagenService = imagenService;
            _paymentService = paymentService;
            _logger = logger;
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
                MetodoPago = "Contra Reembolso",
                StripePublishableKey = _paymentService.GetPublishableKey()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Crea un PaymentIntent de Stripe para el checkout
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent()
        {
            try
            {
                var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
                if (!carritoItems.Any())
                {
                    return Json(new { success = false, error = "Carrito vacío" });
                }

                var total = (decimal)carritoItems.Sum(i => i.Subtotal);
                
                var result = await _paymentService.CreatePaymentIntentAsync(
                    total,
                    "EUR",
                    $"Pedido Alda Joyeros - Usuario {CurrentUser.Id}",
                    new Dictionary<string, string>
                    {
                        { "userId", CurrentUser.Id.ToString() },
                        { "userEmail", CurrentUser.Email }
                    });

                if (result.Success)
                {
                    return Json(new 
                    { 
                        success = true, 
                        clientSecret = result.ClientSecret,
                        paymentIntentId = result.PaymentIntentId
                    });
                }

                return Json(new { success = false, error = result.ErrorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear PaymentIntent");
                return Json(new { success = false, error = "Error al procesar el pago" });
            }
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
                viewModel.StripePublishableKey = _paymentService.GetPublishableKey();
                return View(viewModel);
            }

            try
            {
                // Si es pago con tarjeta, verificar que el pago fue exitoso en Stripe
                if (viewModel.MetodoPago == "Tarjeta" && !string.IsNullOrEmpty(viewModel.PaymentIntentId))
                {
                    var paymentStatus = await _paymentService.GetPaymentStatusAsync(viewModel.PaymentIntentId);
                    
                    if (!paymentStatus.IsSucceeded)
                    {
                        TempData["Error"] = "El pago no se ha completado correctamente. Por favor, inténtalo de nuevo.";
                        return await ReloadCheckoutView(viewModel);
                    }
                    
                    _logger.LogInformation(
                        "Pago confirmado con Stripe: {PaymentIntentId} - Estado: {Status}",
                        viewModel.PaymentIntentId, paymentStatus.Status);
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
                _logger.LogError(ex, "Error al procesar checkout");
                ModelState.AddModelError("", ex.Message);
                return await ReloadCheckoutView(viewModel);
            }
        }

        private async Task<IActionResult> ReloadCheckoutView(CheckoutViewModel viewModel)
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
            viewModel.StripePublishableKey = _paymentService.GetPublishableKey();
            return View(viewModel);
        }
    }
}
