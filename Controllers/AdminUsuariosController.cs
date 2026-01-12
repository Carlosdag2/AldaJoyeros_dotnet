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
            var totalAdmins = todosUsuarios.Count(u => u.Rol == "ADMIN");
            var totalClientes = todosUsuarios.Count(u => u.Rol == "USER");
            
            ViewBag.TotalAdministradores = totalAdmins;
            ViewBag.TotalClientes = totalClientes;
      
            if (!string.IsNullOrEmpty(rol))
            {
                usuariosQuery = usuariosQuery.Where(u => u.Rol == rol).ToList();
            }

            var pagedResult = PagedResult<UsuarioDto>.Create(usuariosQuery, page, PageSize);
            ViewBag.RolFiltro = rol;

            // Si es petición AJAX, devolver partial con estadísticas en headers
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                Response.Headers.Append("X-Stats-Admins", totalAdmins.ToString());
                Response.Headers.Append("X-Stats-Clientes", totalClientes.ToString());
                Response.Headers.Append("X-Stats-Total", pagedResult.TotalItems.ToString());
                Response.Headers.Append("X-Stats-Pagina", $"{pagedResult.PageNumber}/{pagedResult.TotalPages}");
                return PartialView("_UsuariosList", pagedResult);
            }

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

        #region API AJAX

        /// <summary>
        /// Eliminar usuario via AJAX
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> EliminarAjax(long id)
        {
            try
            {
                if (CurrentUser != null && CurrentUser.Id == id)
                {
                    return Json(new { success = false, message = "No puedes eliminar tu propia cuenta" });
                }

                var usuario = await _usuarioService.GetByIdAsync(id);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                await _usuarioService.DeleteAsync(id);
                
                return Json(new { 
                    success = true, 
                    message = $"'{usuario.Email}' eliminado"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cambiar rol de usuario via AJAX
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CambiarRolAjax(long id, string nuevoRol)
        {
            try
            {
                if (CurrentUser != null && CurrentUser.Id == id && nuevoRol != "ADMIN")
                {
                    return Json(new { success = false, message = "No puedes quitarte el rol de admin" });
                }

                var usuario = await _usuarioService.GetByIdAsync(id);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                var updateDto = new UsuarioUpdateDto
                {
                    Email = usuario.Email,
                    Rol = nuevoRol
                };

                await _usuarioService.UpdateAsync(id, updateDto);
                
                return Json(new { 
                    success = true, 
                    message = $"Rol de '{usuario.Email}' cambiado a {nuevoRol}",
                    nuevoRol = nuevoRol
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Buscar usuarios via AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarAjax(string busqueda = "", string rol = "", int page = 1)
        {
            try
            {
                var usuarios = await _usuarioService.GetAllAsync();
                var listaUsuarios = usuarios.ToList();

                if (!string.IsNullOrEmpty(rol))
                {
                    listaUsuarios = listaUsuarios.Where(u => u.Rol == rol).ToList();
                }

                if (!string.IsNullOrWhiteSpace(busqueda))
                {
                    listaUsuarios = listaUsuarios.Where(u => 
                        u.Email.Contains(busqueda, StringComparison.OrdinalIgnoreCase) ||
                        u.Id.ToString().Contains(busqueda)
                    ).ToList();
                }

                var pagedResult = PagedResult<UsuarioDto>.Create(listaUsuarios, page, PageSize);

                var usuariosData = pagedResult.Items.Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    rol = u.Rol,
                    esAdmin = u.Rol == "ADMIN"
                });

                return Json(new
                {
                    success = true,
                    usuarios = usuariosData,
                    currentPage = pagedResult.PageNumber,
                    totalPages = pagedResult.TotalPages,
                    totalItems = pagedResult.TotalItems
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}
