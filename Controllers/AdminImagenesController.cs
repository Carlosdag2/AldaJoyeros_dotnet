using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.DTOs;
using AldaJoyeros.Attributes;
using SkiaSharp;

namespace AldaJoyeros.Controllers;

[JwtAuthorize("ADMIN")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminImagenesController(IProductoImagenService imagenes, IProductoService productos,
    ILogger<AdminImagenesController> logger) : BaseController
{
    private bool Ajax => Request.Headers.XRequestedWith == "XMLHttpRequest";
    private IActionResult Reply(bool success, string message, long productoId, int status = 400)
    {
        if (Ajax) return StatusCode(success ? 200 : status, new { success, message });
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Gestionar", new { id = productoId });
    }

    [HttpGet]
    public async Task<IActionResult> Gestionar(long id)
    {
        var producto = await productos.GetByIdAsync(id);
        if (producto != null) return View(producto);
        TempData["Error"] = "Producto no encontrado.";
        return RedirectToAction("Index", "AdminProductos");
    }

    [HttpGet]
    public async Task<IActionResult> Galeria(long id)
    {
        var producto = await productos.GetByIdAsync(id);
        return producto == null ? NotFound() : PartialView("_Galeria", producto);
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Subir(long productoId, IFormFile? archivo)
    {
        if (archivo == null || archivo.Length == 0) return Reply(false, "Selecciona una imagen.", productoId);
        if (archivo.Length > 5 * 1024 * 1024) return Reply(false, "Cada imagen puede ocupar como máximo 5 MB.", productoId);
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(extension))
            return Reply(false, "Usa una imagen JPG, PNG, WEBP o GIF.", productoId);
        try
        {
            var producto = await productos.GetByIdAsync(productoId);
            if (producto == null) return Reply(false, "El producto ya no existe.", productoId, 404);
            using var stream = new MemoryStream();
            await archivo.CopyToAsync(stream);
            var bytes = stream.ToArray();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            var mime = codec?.EncodedFormat switch {
                SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Webp => "image/webp", SKEncodedImageFormat.Gif => "image/gif", _ => null
            };
            if (mime == null || codec!.Info.Width <= 0 || codec.Info.Height <= 0)
                return Reply(false, "No se puede leer esta imagen. Comprueba que el archivo no esté dañado.", productoId);
            var content = "base64," + Convert.ToBase64String(bytes);
            if (producto.Imagenes.Any(i => i.ImagenBase64.EndsWith(content, StringComparison.Ordinal)))
                return Reply(true, "Esta imagen ya estaba en la galería. No se ha duplicado.", productoId);
            await imagenes.CreateAsync(new ProductoImagenCreateDto {
                ProductoId = productoId, ImagenData = bytes, TipoMime = mime,
                NombreArchivo = Path.GetFileName(archivo.FileName),
                Orden = producto.Imagenes.Count == 0 ? 0 : checked(producto.Imagenes.Max(i => i.Orden) + 1)
            });
            return Reply(true, "Imagen subida.", productoId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al subir imagen del producto {ProductoId}", productoId);
            return Reply(false, "No se pudo confirmar la subida. Actualiza la galería antes de repetirla.", productoId, 500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(string id, long productoId)
    {
        try
        {
            var images = await imagenes.GetByProductoIdAsync(productoId);
            if (!images.Any(i => i.Id == id)) return Reply(false, "La imagen ya no está en este producto. Actualiza la galería.", productoId, 404);
            await imagenes.DeleteByStringIdAsync(id);
            return Reply(true, "Imagen eliminada. Si era la principal, se ha elegido la siguiente disponible.", productoId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al eliminar imagen de {ProductoId}", productoId);
            return Reply(false, "No se pudo confirmar la eliminación. Actualiza la galería.", productoId, 500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EstablecerPrincipal(string id, long productoId)
    {
        try
        {
            var images = await imagenes.GetByProductoIdAsync(productoId);
            if (!images.Any(i => i.Id == id)) return Reply(false, "La imagen ya no está en este producto. Actualiza la galería.", productoId, 404);
            await imagenes.SetAsPrincipalAsync(id);
            return Reply(true, "Imagen principal actualizada.", productoId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al establecer imagen principal de {ProductoId}", productoId);
            return Reply(false, "No se pudo confirmar el cambio. Actualiza la galería.", productoId, 500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Ordenar(long productoId, string[] ids, string[] anterior)
    {
        if (!ModelState.IsValid || ids == null || anterior == null || ids.Length == 0 || anterior.Length == 0) return Reply(false, "Actualiza la galería antes de ordenar.", productoId);
        try
        {
            await imagenes.ReordenarAsync(productoId, ids, anterior);
            return Reply(true, "Orden de las imágenes guardado.", productoId);
        }
        catch (InvalidOperationException ex) { return Reply(false, ex.Message, productoId, 409); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al ordenar imágenes de {ProductoId}", productoId);
            return Reply(false, "No se pudo confirmar el orden. Actualiza la galería.", productoId, 500);
        }
    }
}
