using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers
{
    [JwtAuthorize("ADMIN")]
    public class AdminImagenesController : BaseController
    {
        private readonly IProductoImagenService _imagenService;
        private readonly IProductoService _productoService;

        public AdminImagenesController(
            IProductoImagenService imagenService,
            IProductoService productoService)
        {
            _imagenService = imagenService;
            _productoService = productoService;
        }

        // GET: AdminImagenes/Gestionar/5
        [HttpGet]
        public async Task<IActionResult> Gestionar(long id)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            ViewBag.Producto = producto;
            return View(producto);
        }

        // POST: AdminImagenes/Subir
        [HttpPost]
        public async Task<IActionResult> Subir(long productoId, IFormFile archivo, int orden = 0, bool esPrincipal = false)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    TempData["Error"] = "Debe seleccionar una imagen";
                    return RedirectToAction("Gestionar", new { id = productoId });
                }

                // Validar tipo de archivo
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                
                if (!extensionesPermitidas.Contains(extension))
                {
                    TempData["Error"] = "Formato de imagen no permitido. Use: JPG, PNG, WEBP o GIF";
                    return RedirectToAction("Gestionar", new { id = productoId });
                }

                // Validar tamaño (máximo 5MB)
                if (archivo.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "La imagen no puede superar los 5MB";
                    return RedirectToAction("Gestionar", new { id = productoId });
                }

                var uploadDto = new ProductoImagenUploadDto
                {
                    ProductoId = productoId,
                    Archivo = archivo,
                    Orden = orden,
                    EsPrincipal = esPrincipal
                };

                await _imagenService.CreateFromFileAsync(uploadDto);
                TempData["Success"] = "Imagen subida exitosamente a MongoDB";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al subir la imagen: {ex.Message}";
            }

            return RedirectToAction("Gestionar", new { id = productoId });
        }

        // POST: AdminImagenes/Eliminar
        [HttpPost]
        public async Task<IActionResult> Eliminar(string id, long productoId)
        {
            try
            {
                // El ID de MongoDB es string, usar el método apropiado
                await _imagenService.DeleteByStringIdAsync(id);
                TempData["Success"] = "Imagen eliminada exitosamente de MongoDB";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar la imagen: {ex.Message}";
            }

            return RedirectToAction("Gestionar", new { id = productoId });
        }

        // POST: AdminImagenes/EstablecerPrincipal
        [HttpPost]
        public async Task<IActionResult> EstablecerPrincipal(string id, long productoId)
        {
            try
            {
                // El ID de MongoDB ya es string, usarlo directamente
                await _imagenService.SetAsPrincipalAsync(id);
                TempData["Success"] = "Imagen principal actualizada";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar la imagen principal: {ex.Message}";
            }

            return RedirectToAction("Gestionar", new { id = productoId });
        }
    }
}
