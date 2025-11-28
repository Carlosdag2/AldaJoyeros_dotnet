using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Controllers
{
    public class AdminUsuariosController : BaseController
    {
        private readonly IUsuarioService _usuarioService;
        private const int PageSize = 20;

        public AdminUsuariosController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        public async Task<IActionResult> Index(string rol = "", int page = 1)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            var usuariosQuery = await _usuarioService.GetAllAsync();
            
            // Calcular estadísticas antes de filtrar
            var todosUsuarios = usuariosQuery.ToList();
            ViewBag.TotalAdministradores = todosUsuarios.Count(u => u.Rol == "ADMIN");
            ViewBag.TotalClientes = todosUsuarios.Count(u => u.Rol == "USER" || u.Rol == "CLIENTE");
      
            if (!string.IsNullOrEmpty(rol))
            {
                usuariosQuery = usuariosQuery.Where(u => u.Rol == rol).ToList();
            }

            var pagedResult = PagedResult<UsuarioDto>.Create(usuariosQuery, page, PageSize);
            ViewBag.RolFiltro = rol;

            return View(pagedResult);
        }

        [HttpGet]
        public IActionResult Crear()
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(UsuarioCreateDto usuarioDto)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                return View(usuarioDto);
            }

            try
            {
                await _usuarioService.CreateAsync(usuarioDto);
                TempData["Success"] = "Usuario creado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(usuarioDto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(long id)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            var usuario = await _usuarioService.GetByIdAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }

            var updateDto = new UsuarioUpdateDto
            {
                Email = usuario.Email,
                Rol = usuario.Rol
            };

            ViewBag.UsuarioId = id;
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, UsuarioUpdateDto usuarioDto)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.UsuarioId = id;
                return View(usuarioDto);
            }

            try
            {
                await _usuarioService.UpdateAsync(id, usuarioDto);
                TempData["Success"] = "Usuario actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.UsuarioId = id;
                return View(usuarioDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            if (!IsAuthenticated || !IsAdmin)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                await _usuarioService.DeleteAsync(id);
                TempData["Success"] = "Usuario eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
