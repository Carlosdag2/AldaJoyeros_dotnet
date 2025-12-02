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
        private readonly IJwtService _jwtService;

        public AuthController(IUsuarioService usuarioService, ICarritoService carritoService, IJwtService jwtService)
        {
            _usuarioService = usuarioService;
            _carritoService = carritoService;
            _jwtService = jwtService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto loginDto, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(loginDto);
            }

            try
            {
                var usuario = await _usuarioService.LoginAsync(loginDto);
                
                if (usuario == null)
                {
                    ModelState.AddModelError("", "Email o contraseña incorrectos");
                    ViewBag.ReturnUrl = returnUrl;
                    return View(loginDto);
                }

                // Generar token JWT con toda la información del usuario
                var token = _jwtService.GenerateToken(usuario);
                
                // Almacenar token en cookie HttpOnly segura
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps, // true solo en HTTPS
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(1440) // 24 horas
                };
                
                Response.Cookies.Append("jwt_token", token, cookieOptions);

                // Procesar items del carrito temporal
                await ProcessTempCarrito(usuario.Id);

                // Redirigir según returnUrl o rol
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
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
                ViewBag.ReturnUrl = returnUrl;
                return View(loginDto);
            }
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(UsuarioCreateDto usuarioCreateDto, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
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
                
                if (loggedUser == null)
                {
                    ModelState.AddModelError("", "Error al iniciar sesión automáticamente");
                    ViewBag.ReturnUrl = returnUrl;
                    return View(usuarioCreateDto);
                }
                
                // Generar token JWT
                var token = _jwtService.GenerateToken(loggedUser);
                
                // Almacenar token en cookie HttpOnly
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(1440)
                };
                
                Response.Cookies.Append("jwt_token", token, cookieOptions);

                // Procesar items del carrito temporal
                await ProcessTempCarrito(loggedUser.Id);

                // Redirigir según returnUrl
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
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
                ViewBag.ReturnUrl = returnUrl;
                return View(usuarioCreateDto);
            }
        }

        public IActionResult Logout()
        {
            // Eliminar cookie JWT
            Response.Cookies.Delete("jwt_token");
            
            // Limpiar carrito temporal si existe
            TempCarritoHelper.Clear(HttpContext);
            
            TempData["Success"] = "Sesión cerrada exitosamente";
            return RedirectToAction("Index", "Home");
        }

        private async Task ProcessTempCarrito(long usuarioId)
        {
            var tempItems = TempCarritoHelper.GetItems(HttpContext);
            
            if (tempItems.Any())
            {
                var successCount = 0;
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
                        successCount++;
                    }
                    catch (Exception)
                    {
                        // Si falla agregar algún item, continuar con los demás
                        continue;
                    }
                }
                
                // Limpiar carrito temporal
                TempCarritoHelper.Clear(HttpContext);
                
                if (successCount > 0)
                {
                    TempData["Success"] = $"Se han agregado {successCount} producto(s) a tu carrito.";
                }
            }
        }
    }
}
