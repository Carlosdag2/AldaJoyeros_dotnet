using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Entities;
using System.Security.Cryptography;

namespace AldaJoyeros.Controllers
{
    [ServiceFilter(typeof(AldaJoyeros.Filters.CartSessionFilter))]
    [AutoValidateAntiforgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-forms")]
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
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            if (IsAuthenticated)
            {
                if (returnUrl != null) return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto loginDto, string? returnUrl = null, bool RememberMe = false)
        {
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, completa todos los campos";
                ViewBag.ReturnUrl = returnUrl;
                return View(loginDto);
            }

            try
            {
                var usuario = await _usuarioService.LoginAsync(loginDto);
                
                if (usuario == null)
                {
                    TempData["Error"] = "Email o contraseña incorrectos";
                    ViewBag.ReturnUrl = returnUrl;
                    return View(loginDto);
                }

                var token = _jwtService.GenerateToken(usuario);
                
                var expiration = RememberMe 
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(24);
                
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = expiration
                };
                
                Response.Cookies.Append("jwt_token", token, cookieOptions);

                var hadCart = TempCarritoHelper.HasItems(HttpContext);
                if (!await ProcessTempCarrito(usuario.Id)) return RedirectToAction("Index", "Carrito");

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                if (usuario.Rol == "ADMIN")
                {
                    TempData["Success"] = $"¡Bienvenido de nuevo, {usuario.Email.Split('@')[0]}!";
                    return RedirectToAction("Index", "Admin");
                }

                if (HttpContext.Items[AldaJoyeros.Services.StoreModeStore.ContextKey] is true && hadCart)
                {
                    return RedirectToAction("Index", "Carrito");
                }

                TempData["Success"] = $"¡Bienvenido de nuevo, {usuario.Email.Split('@')[0]}!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al iniciar sesión. Inténtalo de nuevo.";
                _logger.LogError(ex, "Error en login para {Email}", loginDto.Email);
                ViewBag.ReturnUrl = returnUrl;
                return View(loginDto);
            }
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            if (IsAuthenticated)
            {
                if (returnUrl != null) return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(UsuarioCreateDto usuarioCreateDto, string? returnUrl = null)
        {
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
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
                    TempData["Warning"] = "Cuenta creada. Por favor, inicia sesión manualmente.";
                    ViewBag.ReturnUrl = returnUrl;
                    return RedirectToAction("Login", new { returnUrl });
                }
                
                var token = _jwtService.GenerateToken(loggedUser);
                
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(1440)
                };
                
                Response.Cookies.Append("jwt_token", token, cookieOptions);

                var hadCart = TempCarritoHelper.HasItems(HttpContext);
                if (!await ProcessTempCarrito(loggedUser.Id)) return RedirectToAction("Index", "Carrito");

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    TempData["Success"] = $"¡Bienvenido a Alda Joyeros, {loggedUser.Email.Split('@')[0]}! Tu cuenta ha sido creada.";
                    return LocalRedirect(returnUrl);
                }

                if (HttpContext.Items[AldaJoyeros.Services.StoreModeStore.ContextKey] is true && hadCart)
                {
                    TempData["Success"] = "¡Cuenta creada! Ahora puedes completar tu compra.";
                    return RedirectToAction("Index", "Carrito");
                }

                TempData["Success"] = $"¡Bienvenido a Alda Joyeros! Tu cuenta ha sido creada exitosamente.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ya está registrado") || ex.Message.Contains("already"))
                {
                    TempData["Error"] = "Este email ya está registrado. ¿Quieres iniciar sesión?";
                }
                else
                {
                    TempData["Error"] = $"Error al crear la cuenta: {ex.Message}";
                }
                ViewBag.ReturnUrl = returnUrl;
                return View(usuarioCreateDto);
            }
        }

        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt_token");
            TempCarritoHelper.Clear(HttpContext);
            
            TempData["Success"] = "Has cerrado sesión correctamente. ¡Hasta pronto!";
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
                TempData["Warning"] = "Introduce tu email para recuperar la contraseña";
                return View();
            }

            // Protected TempData keeps the same next step even for an unknown address.
            var tokenValue = GenerateSecureToken();
            TempData["Recovery.Token"] = tokenValue;
            TempData["Recovery.Expires"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString();
            TempData["Recovery.Attempts"] = 0;

            try
            {
                var usuario = await _usuarioService.GetByEmailAsync(email);
                
                if (usuario != null)
                {
                    await _passwordResetTokenRepository.InvalidateUserTokensAsync(usuario.Id);
                    
                    var verificationCode = GenerateVerificationCode();
                    
                    var resetToken = new PasswordResetToken
                    {
                        UserId = usuario.Id,
                        Token = tokenValue,
                        Code = verificationCode,
                        ExpiresAt = DateTime.UtcNow.AddHours(1),
                        Used = false,
                        Attempts = 0,
                        CodeVerified = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    await _passwordResetTokenRepository.CreateAsync(resetToken);
                    
                    var verifyLink = Url.Action("VerifyCode", "Auth", new { token = tokenValue }, Request.Scheme);
                    
                    try
                    {
                        await _emailService.SendPasswordResetEmailAsync(email, verifyLink!, verificationCode);
                        _logger.LogInformation("Email de recuperación con código enviado a {Email}", email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar email de recuperación a {Email}", email);
                    }
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ForgotPassword para {Email}", email);
            }

            return RedirectToAction("VerifyCode", new { token = tokenValue });
        }

        [HttpGet]
        public async Task<IActionResult> VerifyCode(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Enlace inválido o expirado";
                return RedirectToAction("Login");
            }

            var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
            
            if (resetToken == null && !HasPendingRecovery(token))
            {
                TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                return RedirectToAction("ForgotPassword");
            }

            if (resetToken?.CodeVerified == true)
            {
                return RedirectToAction("ResetPassword", new { token = token });
            }

            TempData["Info"] = "Si el correo está registrado, recibirás un código. Cópialo aquí o utiliza el enlace del correo. Revisa también la carpeta de spam.";
            ViewBag.Token = token;
            ViewBag.Attempts = resetToken?.Attempts ?? PendingRecoveryAttempts;
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
                TempData["Warning"] = "Introduce el código de 6 dígitos que recibiste por email";
                return View();
            }

            try
            {
                var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(token);
                
                if (resetToken == null)
                {
                    if (HasPendingRecovery(token))
                    {
                        var attempts = PendingRecoveryAttempts;
                        if (attempts >= 5)
                        {
                            ClearPendingRecovery(token);
                            TempData["Error"] = "Has superado el número máximo de intentos. Por seguridad, solicita un nuevo código.";
                            return RedirectToAction("ForgotPassword");
                        }
                        TempData["Recovery.Attempts"] = ++attempts;
                        ViewBag.Attempts = attempts;
                        TempData["Error"] = $"Código incorrecto. Te quedan {5 - attempts} intento(s).";
                        return View();
                    }
                    TempData["Error"] = "El enlace ha expirado o ya fue utilizado. Solicita uno nuevo.";
                    return RedirectToAction("ForgotPassword");
                }

                if (resetToken.Attempts >= 5)
                {
                    resetToken.Used = true;
                    await _passwordResetTokenRepository.UpdateAsync(resetToken);
                    ClearPendingRecovery(token);
                    TempData["Error"] = "Has superado el número máximo de intentos. Por seguridad, solicita un nuevo código.";
                    return RedirectToAction("ForgotPassword");
                }

                resetToken.Attempts++;
                
                if (resetToken.Code != code)
                {
                    await _passwordResetTokenRepository.UpdateAsync(resetToken);
                    var remaining = 5 - resetToken.Attempts;
                    TempData["Error"] = $"Código incorrecto. Te quedan {remaining} intento(s).";
                    ViewBag.Attempts = resetToken.Attempts;
                    return View();
                }

                resetToken.CodeVerified = true;
                await _passwordResetTokenRepository.UpdateAsync(resetToken);
                ClearPendingRecovery(token);
                
                _logger.LogInformation("Código verificado correctamente para usuario {UserId}", resetToken.UserId);
                
                TempData["Success"] = "¡Código verificado! Ahora puedes establecer tu nueva contraseña.";
                return RedirectToAction("ResetPassword", new { token = token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código");
                TempData["Error"] = "Error al verificar el código. Inténtalo de nuevo.";
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
                TempData["Warning"] = "La contraseña debe tener al menos 6 caracteres";
                return View();
            }

            if (password != confirmPassword)
            {
                TempData["Error"] = "Las contraseñas no coinciden";
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

                if (!resetToken.CodeVerified)
                {
                    return RedirectToAction("VerifyCode", new { token = token });
                }

                await _usuarioService.UpdatePasswordAsync(resetToken.UserId, password);
                
                resetToken.Used = true;
                await _passwordResetTokenRepository.UpdateAsync(resetToken);
                
                _logger.LogInformation("Contraseña restablecida para usuario {UserId}", resetToken.UserId);
                
                TempData["Success"] = "¡Contraseña restablecida exitosamente! Ya puedes iniciar sesión con tu nueva contraseña.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer contraseña");
                TempData["Error"] = "Error al restablecer la contraseña. Inténtalo de nuevo.";
                return View();
            }
        }

        private int PendingRecoveryAttempts => TempData.Peek("Recovery.Attempts") is int attempts ? attempts : 0;

        private bool HasPendingRecovery(string token)
        {
            return TempData.Peek("Recovery.Token") is string pending && pending == token
                && long.TryParse(TempData.Peek("Recovery.Expires") as string, out var expires)
                && expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void ClearPendingRecovery(string token)
        {
            if (TempData.Peek("Recovery.Token") as string != token) return;
            TempData.Remove("Recovery.Token");
            TempData.Remove("Recovery.Expires");
            TempData.Remove("Recovery.Attempts");
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
            return number.ToString("D6");
        }

        private async Task<bool> ProcessTempCarrito(long usuarioId)
        {
            if (HttpContext.Items[AldaJoyeros.Services.StoreModeStore.ContextKey] is not true || !TempCarritoHelper.HasItems(HttpContext)) return true;
            var cart = HttpContext.RequestServices.GetRequiredService<AldaJoyeros.Services.GuestCartService>();
            var saved = await cart.MergeAsync(usuarioId);
            if (cart.Notices.Count > 0) TempData["Warning"] = string.Join(" ", cart.Notices.Distinct());
            return saved;
        }
    }
}
