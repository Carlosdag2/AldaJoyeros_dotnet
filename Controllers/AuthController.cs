using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Entities;
using System.Security.Cryptography;

namespace AldaJoyeros.Controllers
{
    public class AuthController : BaseController
    {
        private readonly IUsuarioService _usuarioService;
        private readonly ICarritoService _carritoService;
        private readonly IJwtService _jwtService;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUsuarioService usuarioService, 
            ICarritoService carritoService, 
            IJwtService jwtService,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _usuarioService = usuarioService;
            _carritoService = carritoService;
            _jwtService = jwtService;
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _emailService = emailService;
            _logger = logger;
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
        public async Task<IActionResult> Login(LoginDto loginDto, string? returnUrl = null, bool RememberMe = false)
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
                
                // Configurar duración de la cookie según RememberMe
                var expiration = RememberMe 
                    ? DateTimeOffset.UtcNow.AddDays(30)  // 30 días si marca "Mantener sesión"
                    : DateTimeOffset.UtcNow.AddHours(24); // 24 horas por defecto
                
                // Almacenar token en cookie HttpOnly segura
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps, // true solo en HTTPS
                    SameSite = SameSiteMode.Strict,
                    Expires = expiration
                };
                
                Response.Cookies.Append("jwt_token", token, cookieOptions);

                // Procesar items del carrito temporal
                await ProcessTempCarrito(usuario.Id);

                // Redirigir seg�n returnUrl o rol
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (usuario.Rol == "ADMIN")
                {
                    return RedirectToAction("Index", "Admin");
                }

                // Si hab�a items en el carrito temporal, ir al carrito
                if (TempCarritoHelper.HasItems(HttpContext))
                {
                    return RedirectToAction("Index", "Carrito");
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al iniciar sesi�n: " + ex.Message);
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
                    ModelState.AddModelError("", "Error al iniciar sesi�n autom�ticamente");
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

                // Redirigir seg�n returnUrl
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Si hab�a items en el carrito temporal, ir al carrito
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

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "El email es obligatorio");
                return View();
            }

            try
            {
                var usuario = await _usuarioService.GetByEmailAsync(email);
                
                if (usuario != null)
                {
                    // Invalidar tokens anteriores del usuario
                    await _passwordResetTokenRepository.InvalidateUserTokensAsync(usuario.Id);
                    
                    // Generar token único y código de 6 dígitos
                    var tokenValue = GenerateSecureToken();
                    var verificationCode = GenerateVerificationCode();
                    
                    // Crear token en base de datos
                    var resetToken = new PasswordResetToken
                    {
                        UserId = usuario.Id,
                        Token = tokenValue,
                        Code = verificationCode,
                        ExpiresAt = DateTime.UtcNow.AddHours(1), // 1 hora para el código
                        Used = false,
                        Attempts = 0,
                        CodeVerified = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    await _passwordResetTokenRepository.CreateAsync(resetToken);
                    
                    // Generar enlace de verificación (lleva al usuario a la página para introducir el código)
                    var verifyLink = Url.Action("VerifyCode", "Auth", new { token = tokenValue }, Request.Scheme);
                    
                    // Enviar email con código
                    try
                    {
                        await _emailService.SendPasswordResetEmailAsync(email, verifyLink!, verificationCode);
                        _logger.LogInformation("Email de recuperación con código enviado a {Email}", email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar email de recuperación a {Email}", email);
                        // No revelamos el error al usuario por seguridad
                    }
                }
                
                // Siempre mostramos el mismo mensaje por seguridad
                TempData["Success"] = "Si el email está registrado, recibirás instrucciones para restablecer tu contraseña.";
                TempData["EmailSent"] = true;
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ForgotPassword para {Email}", email);
                TempData["Success"] = "Si el email está registrado, recibirás instrucciones para restablecer tu contraseña.";
                TempData["EmailSent"] = true;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerifyCode(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Enlace inválido";
                return RedirectToAction("Login");
            }

            var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
            
            if (resetToken == null)
            {
                TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                return RedirectToAction("ForgotPassword");
            }

            // Si ya se verificó el código, redirigir a resetear contraseña
            if (resetToken.CodeVerified)
            {
                return RedirectToAction("ResetPassword", new { token = token });
            }

            ViewBag.Token = token;
            ViewBag.Attempts = resetToken.Attempts;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyCode(string token, string code)
        {
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Enlace inválido";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            {
                ModelState.AddModelError("", "Introduce el código de 6 dígitos");
                return View();
            }

            try
            {
                var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
                
                if (resetToken == null)
                {
                    TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                    return RedirectToAction("ForgotPassword");
                }

                // Verificar número de intentos (máximo 5)
                if (resetToken.Attempts >= 5)
                {
                    resetToken.Used = true;
                    await _passwordResetTokenRepository.UpdateAsync(resetToken);
                    TempData["Error"] = "Has superado el número máximo de intentos. Solicita un nuevo código.";
                    return RedirectToAction("ForgotPassword");
                }

                // Incrementar intentos
                resetToken.Attempts++;
                
                // Verificar código
                if (resetToken.Code != code)
                {
                    await _passwordResetTokenRepository.UpdateAsync(resetToken);
                    var remaining = 5 - resetToken.Attempts;
                    ModelState.AddModelError("", $"Código incorrecto. Te quedan {remaining} intento(s).");
                    ViewBag.Attempts = resetToken.Attempts;
                    return View();
                }

                // Código correcto - marcar como verificado
                resetToken.CodeVerified = true;
                await _passwordResetTokenRepository.UpdateAsync(resetToken);
                
                _logger.LogInformation("Código verificado correctamente para usuario {UserId}", resetToken.UserId);
                
                // Redirigir a cambiar contraseña
                return RedirectToAction("ResetPassword", new { token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código");
                ModelState.AddModelError("", "Error al verificar el código. Inténtalo de nuevo.");
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Enlace inválido";
                return RedirectToAction("Login");
            }

            var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
            
            if (resetToken == null)
            {
                TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                return RedirectToAction("ForgotPassword");
            }

            // Verificar que el código ya fue validado
            if (!resetToken.CodeVerified)
            {
                return RedirectToAction("VerifyCode", new { token = token });
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string password, string confirmPassword)
        {
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Enlace inválido";
                return RedirectToAction("Login");
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ModelState.AddModelError("", "La contraseña debe tener al menos 6 caracteres");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Las contraseñas no coinciden");
                return View();
            }

            try
            {
                var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
                
                if (resetToken == null)
                {
                    TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                    return RedirectToAction("ForgotPassword");
                }

                // Verificar que el código fue validado
                if (!resetToken.CodeVerified)
                {
                    return RedirectToAction("VerifyCode", new { token = token });
                }

                // Actualizar contraseña del usuario
                await _usuarioService.UpdatePasswordAsync(resetToken.UserId, password);
                
                // Marcar token como usado
                resetToken.Used = true;
                await _passwordResetTokenRepository.UpdateAsync(resetToken);
                
                _logger.LogInformation("Contraseña restablecida para usuario {UserId}", resetToken.UserId);
                
                TempData["Success"] = "¡Contraseña restablecida exitosamente! Ya puedes iniciar sesión.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer contraseña");
                ModelState.AddModelError("", "Error al restablecer la contraseña. Inténtalo de nuevo.");
                return View();
            }
        }

        private static string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private static string GenerateVerificationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = BitConverter.ToUInt32(bytes, 0) % 1000000;
            return number.ToString("D6"); // Siempre 6 dígitos con ceros a la izquierda
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
                        // Si falla agregar alg�n item, continuar con los dem�s
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
