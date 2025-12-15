using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Attributes;
using System.Text.Json;

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
        private readonly IEmailService _emailService;
        private readonly IFacturaService _facturaService;
        private readonly ILogger<PedidosController> _logger;

        private const string CheckoutSessionKey = "CheckoutData";

        public PedidosController(
            IPedidoService pedidoService,
            ICarritoService carritoService,
            IDireccionService direccionService,
            IProductoImagenService imagenService,
            IPaymentService paymentService,
            IEmailService emailService,
            IFacturaService facturaService,
            ILogger<PedidosController> logger)
        {
            _pedidoService = pedidoService;
            _carritoService = carritoService;
            _direccionService = direccionService;
            _imagenService = imagenService;
            _paymentService = paymentService;
            _emailService = emailService;
            _facturaService = facturaService;
            _logger = logger;
        }

        #region Mis Pedidos

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

        #endregion

        #region Checkout - Paso 1: Dirección

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            // Limpiar sesión de checkout anterior
            ClearCheckoutSession();

            var carritoItems = await GetCarritoConImagenes();
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            // Obtener direcciones guardadas del usuario
            var direcciones = await _direccionService.GetByUsuarioIdAsync(CurrentUser!.Id);

            var viewModel = new CheckoutDireccionViewModel
            {
                CarritoItems = carritoItems,
                DireccionesGuardadas = direcciones,
                UsarNuevaDireccion = !direcciones.Any()
            };

            return View("Checkout_Direccion", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarDireccion(
            CheckoutDireccionViewModel viewModel,
            long? DireccionSeleccionadaId = null,
            bool UsarNuevaDireccion = false,
            bool GuardarDireccion = false)
        {
            var carritoItems = await GetCarritoConImagenes();
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            DireccionDto direccionParaPedido;

            try
            {
                // Opción 1: Usar dirección guardada seleccionada
                if (DireccionSeleccionadaId.HasValue && !UsarNuevaDireccion)
                {
                    var direccionGuardada = await _direccionService.GetByIdAsync(DireccionSeleccionadaId.Value);
                    if (direccionGuardada == null || direccionGuardada.UsuarioId != CurrentUser!.Id)
                    {
                        TempData["Error"] = "Dirección no válida";
                        return RedirectToAction("Checkout");
                    }
                    direccionParaPedido = direccionGuardada;
                    _logger.LogInformation("Usuario {UserId} usa dirección guardada {DireccionId}", CurrentUser.Id, DireccionSeleccionadaId);
                }
                // Opción 2: Usar nueva dirección
                else
                {
                    // Validar campos obligatorios
                    if (string.IsNullOrWhiteSpace(viewModel.NuevaDireccion.Calle) ||
                        string.IsNullOrWhiteSpace(viewModel.NuevaDireccion.Numero) ||
                        string.IsNullOrWhiteSpace(viewModel.NuevaDireccion.CodigoPostal) ||
                        string.IsNullOrWhiteSpace(viewModel.NuevaDireccion.Ciudad) ||
                        string.IsNullOrWhiteSpace(viewModel.NuevaDireccion.Provincia))
                    {
                        ModelState.AddModelError("", "Por favor, completa todos los campos obligatorios de la dirección");
                        viewModel.CarritoItems = carritoItems;
                        viewModel.DireccionesGuardadas = await _direccionService.GetByUsuarioIdAsync(CurrentUser!.Id);
                        return View("Checkout_Direccion", viewModel);
                    }

                    // Si quiere guardar la dirección para futuras compras
                    if (GuardarDireccion)
                    {
                        try
                        {
                            var direccionGuardada = await _direccionService.CreateAsync(CurrentUser!.Id, viewModel.NuevaDireccion);
                            _logger.LogInformation("Nueva dirección guardada para usuario {UserId}: {DireccionId}", CurrentUser.Id, direccionGuardada.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudo guardar la dirección del usuario, continuando sin guardar");
                        }
                    }

                    // Crear DTO para el pedido (independiente de si se guardó o no)
                    direccionParaPedido = new DireccionDto
                    {
                        Calle = viewModel.NuevaDireccion.Calle,
                        Numero = viewModel.NuevaDireccion.Numero,
                        Piso = viewModel.NuevaDireccion.Piso,
                        CodigoPostal = viewModel.NuevaDireccion.CodigoPostal,
                        Ciudad = viewModel.NuevaDireccion.Ciudad,
                        Provincia = viewModel.NuevaDireccion.Provincia
                    };
                }

                // Guardar en sesión para el siguiente paso
                var sessionData = new CheckoutSessionData
                {
                    Direccion = direccionParaPedido,
                    DireccionConfirmada = true
                };
                SaveCheckoutSession(sessionData);

                return RedirectToAction("CheckoutPago");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar dirección");
                ModelState.AddModelError("", "Error al procesar la dirección. Por favor, inténtalo de nuevo.");
                viewModel.CarritoItems = carritoItems;
                viewModel.DireccionesGuardadas = await _direccionService.GetByUsuarioIdAsync(CurrentUser!.Id);
                return View("Checkout_Direccion", viewModel);
            }
        }

        #endregion

        #region Checkout - Paso 2: Método de Pago

        [HttpGet]
        public async Task<IActionResult> CheckoutPago()
        {
            var sessionData = GetCheckoutSession();
            if (sessionData?.Direccion == null || !sessionData.DireccionConfirmada)
            {
                return RedirectToAction("Checkout");
            }

            var carritoItems = await GetCarritoConImagenes();
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            var viewModel = new CheckoutPagoViewModel
            {
                CarritoItems = carritoItems,
                Direccion = sessionData.Direccion,
                StripePublishableKey = _paymentService.GetPublishableKey()
            };

            return View("Checkout_Pago", viewModel);
        }

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPago(CheckoutPagoViewModel viewModel)
        {
            var sessionData = GetCheckoutSession();
            if (sessionData?.Direccion == null || !sessionData.DireccionConfirmada)
            {
                return RedirectToAction("Checkout");
            }

            var carritoItems = await GetCarritoConImagenes();
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            if (string.IsNullOrEmpty(viewModel.MetodoPago))
            {
                viewModel.CarritoItems = carritoItems;
                viewModel.Direccion = sessionData.Direccion;
                viewModel.StripePublishableKey = _paymentService.GetPublishableKey();
                ModelState.AddModelError("", "Selecciona un método de pago");
                return View("Checkout_Pago", viewModel);
            }

            if (viewModel.MetodoPago == "Tarjeta")
            {
                if (string.IsNullOrEmpty(viewModel.PaymentIntentId))
                {
                    viewModel.CarritoItems = carritoItems;
                    viewModel.Direccion = sessionData.Direccion;
                    viewModel.StripePublishableKey = _paymentService.GetPublishableKey();
                    ModelState.AddModelError("", "Por favor, completa el pago con tarjeta");
                    return View("Checkout_Pago", viewModel);
                }

                var paymentStatus = await _paymentService.GetPaymentStatusAsync(viewModel.PaymentIntentId);
                if (!paymentStatus.IsSucceeded)
                {
                    viewModel.CarritoItems = carritoItems;
                    viewModel.Direccion = sessionData.Direccion;
                    viewModel.StripePublishableKey = _paymentService.GetPublishableKey();
                    ModelState.AddModelError("", "El pago no se completó correctamente");
                    return View("Checkout_Pago", viewModel);
                }
            }

            sessionData.MetodoPago = viewModel.MetodoPago;
            sessionData.PaymentIntentId = viewModel.PaymentIntentId;
            sessionData.PagoConfirmado = true;
            SaveCheckoutSession(sessionData);

            return RedirectToAction("CheckoutConfirmacion");
        }

        #endregion

        #region Checkout - Paso 3: Confirmación

        [HttpGet]
        public async Task<IActionResult> CheckoutConfirmacion()
        {
            var sessionData = GetCheckoutSession();
            if (sessionData?.Direccion == null || !sessionData.DireccionConfirmada)
            {
                return RedirectToAction("Checkout");
            }

            if (string.IsNullOrEmpty(sessionData.MetodoPago) || !sessionData.PagoConfirmado)
            {
                return RedirectToAction("CheckoutPago");
            }

            var carritoItems = await GetCarritoConImagenes();
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            var viewModel = new CheckoutConfirmacionViewModel
            {
                CarritoItems = carritoItems,
                Direccion = sessionData.Direccion,
                MetodoPago = sessionData.MetodoPago,
                PaymentIntentId = sessionData.PaymentIntentId
            };

            return View("Checkout_Confirmacion", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarPedido()
        {
            var sessionData = GetCheckoutSession();
            if (sessionData?.Direccion == null || !sessionData.DireccionConfirmada ||
                string.IsNullOrEmpty(sessionData.MetodoPago) || !sessionData.PagoConfirmado)
            {
                return RedirectToAction("Checkout");
            }

            var carritoItems = await _carritoService.GetByUsuarioIdAsync(CurrentUser!.Id);
            if (!carritoItems.Any())
            {
                TempData["Error"] = "Tu carrito está vacío";
                return RedirectToAction("Index", "Carrito");
            }

            try
            {
                if (sessionData.MetodoPago == "Tarjeta" && !string.IsNullOrEmpty(sessionData.PaymentIntentId))
                {
                    var paymentStatus = await _paymentService.GetPaymentStatusAsync(sessionData.PaymentIntentId);
                    if (!paymentStatus.IsSucceeded)
                    {
                        TempData["Error"] = "El pago no se completó correctamente";
                        return RedirectToAction("CheckoutPago");
                    }
                }

                // Crear pedido
                var pedidoDto = new PedidoCreateDto
                {
                    Direccion = new DireccionCreateDto
                    {
                        Calle = sessionData.Direccion.Calle,
                        Numero = sessionData.Direccion.Numero,
                        Piso = sessionData.Direccion.Piso,
                        CodigoPostal = sessionData.Direccion.CodigoPostal,
                        Ciudad = sessionData.Direccion.Ciudad,
                        Provincia = sessionData.Direccion.Provincia
                    }
                };

                var pedido = await _pedidoService.CreateFromCarritoAsync(CurrentUser!.Id, pedidoDto);
                
                ClearCheckoutSession();

                _logger.LogInformation(
                    "Pedido {PedidoId} creado para usuario {UserId} - Método: {MetodoPago}",
                    pedido.Id, CurrentUser.Id, sessionData.MetodoPago);

                // Enviar email de confirmación con factura adjunta
                try
                {
                    var facturaPdf = await _facturaService.GenerarFacturaPdfAsync(pedido, CurrentUser.Email, CurrentUser.Email);
                    var numeroFactura = _facturaService.GenerarNumeroFactura(pedido.Id);
                    
                    await _emailService.SendOrderConfirmationWithInvoiceAsync(CurrentUser.Email, pedido, facturaPdf, numeroFactura);
                    _logger.LogInformation("Email de confirmación con factura {NumeroFactura} enviado para pedido {PedidoId}", numeroFactura, pedido.Id);
                }
                catch (Exception emailEx)
                {
                    // No fallar el pedido si el email no se envía
                    _logger.LogWarning(emailEx, "No se pudo enviar email de confirmación con factura para pedido {PedidoId}", pedido.Id);
                }

                TempData["Success"] = sessionData.MetodoPago == "Contra Reembolso" 
                    ? "¡Pedido realizado! Pagarás al recibir tu pedido. Te hemos enviado la confirmación y factura por email." 
                    : "¡Pedido realizado y pago procesado exitosamente! Te hemos enviado la confirmación y factura por email.";
                
                return RedirectToAction("PedidoCompletado", new { id = pedido.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear pedido");
                TempData["Error"] = "Error al procesar el pedido: " + ex.Message;
                return RedirectToAction("CheckoutConfirmacion");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PedidoCompletado(long id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null || pedido.UsuarioId != CurrentUser!.Id)
            {
                return RedirectToAction("Index");
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

        #endregion

        #region Helpers

        private async Task<IEnumerable<CarritoItemDto>> GetCarritoConImagenes()
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

            return carritoItems;
        }

        private CheckoutSessionData? GetCheckoutSession()
        {
            var json = HttpContext.Session.GetString(CheckoutSessionKey);
            if (string.IsNullOrEmpty(json)) return null;
            
            return JsonSerializer.Deserialize<CheckoutSessionData>(json);
        }

        private void SaveCheckoutSession(CheckoutSessionData data)
        {
            var json = JsonSerializer.Serialize(data);
            HttpContext.Session.SetString(CheckoutSessionKey, json);
        }

        private void ClearCheckoutSession()
        {
            HttpContext.Session.Remove(CheckoutSessionKey);
        }

        #endregion
    }
}
