using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class AuthController : BaseController
    {
        private readonly IUsuarioService _usuarioService;
        private readonly ICarritoService _carritoService;

        public AuthController(IUsuarioService usuarioService, ICarritoService carritoService)
        {
            _usuarioService = usuarioService;
            _carritoService = carritoService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return View(loginDto);
            }

            try
            {
                var usuario = await _usuarioService.LoginAsync(loginDto);
                
                if (usuario == null)
                {
                    ModelState.AddModelError("", "Email o contraseña incorrectos");
                    return View(loginDto);
                }

                HttpContext.Session.SetObject("CurrentUser", usuario);

                // Procesar items del carrito temporal
                await ProcessTempCarrito(usuario.Id);

                // Verificar si hay URL de retorno
                var returnUrl = HttpContext.Session.GetString("ReturnUrl");
                HttpContext.Session.Remove("ReturnUrl");

                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (usuario.Rol == "ADMIN")
                {
                    return RedirectToAction("Index", "Admin");
                }

                // Si había items en el carrito temporal, ir al carrito
                if (TempCarritoHelper.HasItems(HttpContext))
                {
                    return RedirectToAction("Index", "Carrito");
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al iniciar sesión: " + ex.Message);
                return View(loginDto);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(UsuarioCreateDto usuarioCreateDto)
        {
            if (!ModelState.IsValid)
            {
                return View(usuarioCreateDto);
            }

            try
            {
                usuarioCreateDto.Rol = "USER";
                var usuario = await _usuarioService.CreateAsync(usuarioCreateDto);
                
                var loginDto = new LoginDto
                {
                    Email = usuarioCreateDto.Email,
                    Password = usuarioCreateDto.Password
                };

                var loggedUser = await _usuarioService.LoginAsync(loginDto);
                HttpContext.Session.SetObject("CurrentUser", loggedUser);

                // Procesar items del carrito temporal
                await ProcessTempCarrito(loggedUser!.Id);

                // Verificar si hay URL de retorno
                var returnUrl = HttpContext.Session.GetString("ReturnUrl");
                HttpContext.Session.Remove("ReturnUrl");

                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Si había items en el carrito temporal, ir al carrito
                if (TempCarritoHelper.HasItems(HttpContext))
                {
                    return RedirectToAction("Index", "Carrito");
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(usuarioCreateDto);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        private async Task ProcessTempCarrito(long usuarioId)
        {
            var tempItems = TempCarritoHelper.GetItems(HttpContext);
            
            if (tempItems.Any())
            {
                foreach (var item in tempItems)
                {
                    try
                    {
                        var carritoItemDto = new CarritoItemCreateDto
                        {
                            ProductoId = item.ProductoId,
                            Cantidad = item.Cantidad
                        };

                        await _carritoService.AddItemAsync(usuarioId, carritoItemDto);
                    }
                    catch (Exception)
                    {
                        // Si falla agregar algún item, continuar con los demás
                        continue;
                    }
                }
                
                // Limpiar carrito temporal
                TempCarritoHelper.Clear(HttpContext);
                
                TempData["Success"] = $"Se han agregado {tempItems.Count} producto(s) a tu carrito.";
            }
        }
    }
}
