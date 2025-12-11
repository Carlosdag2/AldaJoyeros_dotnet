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
                TempData["Error"] = "Producto no encontrado";
                return RedirectToAction("Index", "AdminProductos");
            }

            if (producto.Imagenes == null || !producto.Imagenes.Any())
            {
                TempData["Info"] = "Este producto aún no tiene imágenes. ¡Añade algunas!";
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
                    TempData["Warning"] = "Debes seleccionar una imagen para subir";
                    return RedirectToAction("Gestionar", new { id = productoId });
                }

                // Validar tipo de archivo
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                
                if (!extensionesPermitidas.Contains(extension))
                {
                    TempData["Error"] = "Formato de imagen no permitido. Usa: JPG, PNG, WEBP o GIF";
                    return RedirectToAction("Gestionar", new { id = productoId });
                }

                // Validar tamaño (máximo 5MB)
                if (archivo.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "La imagen es demasiado grande. Máximo permitido: 5MB";
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
                TempData["Success"] = $"Imagen '{archivo.FileName}' subida exitosamente";
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
                TempData["Success"] = "Imagen eliminada exitosamente";
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
                TempData["Success"] = "Imagen principal actualizada. Esta imagen se mostrará primero en el catálogo.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al actualizar la imagen principal: {ex.Message}";
            }

            return RedirectToAction("Gestionar", new { id = productoId });
        }
    }
}
