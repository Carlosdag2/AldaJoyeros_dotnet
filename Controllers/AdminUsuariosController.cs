using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
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
            var usuariosQuery = await _usuarioService.GetAllAsync();
            
            var todosUsuarios = usuariosQuery.ToList();
            ViewBag.TotalAdministradores = todosUsuarios.Count(u => u.Rol == "ADMIN");
            ViewBag.TotalClientes = todosUsuarios.Count(u => u.Rol == "USER");
      
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
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Crear(UsuarioCreateDto usuarioDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                return View(usuarioDto);
            }

            try
            {
                await _usuarioService.CreateAsync(usuarioDto);
                TempData["Success"] = $"Usuario '{usuarioDto.Email}' creado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear el usuario: {ex.Message}";
                return View(usuarioDto);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Editar(long id)
        {
            var usuario = await _usuarioService.GetByIdAsync(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index");
            }

            // Verificar si intenta editar su propia cuenta
            if (CurrentUser != null && CurrentUser.Id == id)
            {
                TempData["Warning"] = "Estás editando tu propia cuenta. Ten cuidado al cambiar el rol.";
            }

            var updateDto = new UsuarioUpdateDto
            {
                Email = usuario.Email,
                Rol = usuario.Rol
            };

            ViewBag.UsuarioId = id;
            ViewBag.UsuarioEmail = usuario.Email;
            return View(updateDto);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(long id, UsuarioUpdateDto usuarioDto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Warning"] = "Por favor, revisa los campos del formulario";
                ViewBag.UsuarioId = id;
                return View(usuarioDto);
            }

            try
            {
                // Verificar si está quitándose el rol de admin a sí mismo
                if (CurrentUser != null && CurrentUser.Id == id && usuarioDto.Rol != "ADMIN")
                {
                    TempData["Error"] = "No puedes quitarte el rol de administrador a ti mismo";
                    ViewBag.UsuarioId = id;
                    return View(usuarioDto);
                }

                await _usuarioService.UpdateAsync(id, usuarioDto);
                TempData["Success"] = $"Usuario '{usuarioDto.Email}' actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar el usuario: {ex.Message}";
                ViewBag.UsuarioId = id;
                return View(usuarioDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Eliminar(long id)
        {
            try
            {
                // No permitir eliminarse a sí mismo
                if (CurrentUser != null && CurrentUser.Id == id)
                {
                    TempData["Error"] = "No puedes eliminar tu propia cuenta";
                    return RedirectToAction("Index");
                }

                var usuario = await _usuarioService.GetByIdAsync(id);
                var emailUsuario = usuario?.Email ?? "El usuario";
                
                await _usuarioService.DeleteAsync(id);
                TempData["Success"] = $"Usuario '{emailUsuario}' eliminado exitosamente";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"No se pudo eliminar el usuario: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
