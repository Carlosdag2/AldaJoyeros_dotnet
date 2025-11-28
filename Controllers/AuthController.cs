using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class AuthController : BaseController
    {
        private readonly IUsuarioService _usuarioService;

        public AuthController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
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

                if (usuario.Rol == "ADMIN")
                {
                    return RedirectToAction("Index", "Admin");
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
    }
}
